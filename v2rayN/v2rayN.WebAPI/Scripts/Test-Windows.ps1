param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$PublishDirectory
)

$ErrorActionPreference = 'Stop'
$packageRoot = (Resolve-Path -LiteralPath $PublishDirectory).Path
$sourceExe = Join-Path $packageRoot 'v2rayN.WebAPI.exe'
if (-not (Test-Path -LiteralPath $sourceExe -PathType Leaf)) { throw "Missing executable: $sourceExe" }
if (-not [Environment]::Is64BitOperatingSystem) { throw 'This package requires 64-bit Windows.' }

$testRoot = Join-Path $env:TEMP ('v2rayN.WebAPI-win-x64-smoke-' + [Guid]::NewGuid().ToString('N'))
$runtimeDir = Join-Path $testRoot 'runtime'
New-Item -ItemType Directory -Path $runtimeDir -Force | Out-Null
$webProcess = $null
$processInfo = $null
$nativeHandlesOpen = $false
$client = $null
$success = $false
$oldEnvironment = @{}
$environmentNames = @(
    'V2RAYN_DATA_HOME', 'V2RAYN_LOCAL_APPLICATION_DATA_V2', 'V2RAYN_WEB_API_KEY',
    'INVOCATION_ID', 'JOURNAL_STREAM', 'NOTIFY_SOCKET', 'DOTNET_RUNNING_IN_CONTAINER', 'container'
)

try {
    Get-ChildItem -LiteralPath $packageRoot -Force | Where-Object {
        $_.Name -notin @('README.txt', 'Test-Windows.ps1', '.env')
    } | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $runtimeDir -Recurse -Force
    }

    foreach ($name in $environmentNames) {
        $oldEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        [Environment]::SetEnvironmentVariable($name, $null, 'Process')
    }

    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    $listener.Stop()
    $url = "http://127.0.0.1:$port"

    if (-not ('WebApiWindowsSmokeNative' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class WebApiWindowsSmokeNative
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct STARTUPINFO
    {
        public int cb;
        public IntPtr lpReserved;
        public IntPtr lpDesktop;
        public IntPtr lpTitle;
        public uint dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_INFORMATION
    {
        public IntPtr hProcess, hThread;
        public uint dwProcessId, dwThreadId;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool CreateProcess(string applicationName, StringBuilder commandLine,
        IntPtr processAttributes, IntPtr threadAttributes, bool inheritHandles, uint creationFlags,
        IntPtr environment, string currentDirectory, ref STARTUPINFO startupInfo,
        out PROCESS_INFORMATION processInformation);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GenerateConsoleCtrlEvent(uint controlEvent, uint processGroupId);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr handle);
}
'@
    }

    $exe = Join-Path $runtimeDir 'v2rayN.WebAPI.exe'
    $commandLine = '"{0}" --foreground --no-open --urls "{1}"' -f $exe, $url
    $startupInfo = [WebApiWindowsSmokeNative+STARTUPINFO]::new()
    $startupInfo.cb = [Runtime.InteropServices.Marshal]::SizeOf([type][WebApiWindowsSmokeNative+STARTUPINFO])
    $processInfo = [WebApiWindowsSmokeNative+PROCESS_INFORMATION]::new()
    $created = [WebApiWindowsSmokeNative]::CreateProcess(
        $exe, [System.Text.StringBuilder]::new($commandLine), [IntPtr]::Zero, [IntPtr]::Zero,
        $true, [uint32]0x00000200, [IntPtr]::Zero, $runtimeDir,
        [ref]$startupInfo, [ref]$processInfo)
    if (-not $created) {
        $code = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
        throw "CreateProcess failed with Win32 error $code."
    }
    $nativeHandlesOpen = $true
    $webProcess = [Diagnostics.Process]::GetProcessById([int]$processInfo.dwProcessId)
    [void][WebApiWindowsSmokeNative]::CloseHandle($processInfo.hThread)
    [void][WebApiWindowsSmokeNative]::CloseHandle($processInfo.hProcess)
    $nativeHandlesOpen = $false

    Add-Type -AssemblyName System.Net.Http
    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.UseProxy = $false
    $client = [System.Net.Http.HttpClient]::new($handler)
    $healthUrl = "$url/api/health"
    $health = $null
    $lastRequestError = $null
    for ($attempt = 0; $attempt -lt 90; $attempt++) {
        if ($webProcess.HasExited) { throw "WebAPI exited during startup (code $($webProcess.ExitCode))." }
        try {
            $response = $client.GetAsync($healthUrl).GetAwaiter().GetResult()
            $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            if ([int]$response.StatusCode -eq 200) {
                $health = $body | ConvertFrom-Json
                if ($health.status -ne 'ok') { throw "Unexpected health response: $body" }
                break
            }
            $lastRequestError = "HTTP $([int]$response.StatusCode): $body"
        } catch {
            $lastRequestError = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 300
    }
    if ($null -eq $health) { throw "Health check failed at $healthUrl. Last error: $lastRequestError" }

    $lockPath = Join-Path $runtimeDir 'v2rayN.WebAPI.instance.lock'
    $webApiDataDir = Join-Path $runtimeDir 'WebAPIData'
    $tempDataDir = Join-Path $runtimeDir 'guiTemps'
    foreach ($path in @($lockPath, $webApiDataDir, $tempDataDir)) {
        if (-not (Test-Path -LiteralPath $path)) { throw "First-startup data path was not created: $path" }
    }

    if (-not [WebApiWindowsSmokeNative]::GenerateConsoleCtrlEvent(1, $processInfo.dwProcessId)) {
        $code = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
        throw "Could not send graceful CTRL_BREAK_EVENT (Win32 error $code)."
    }
    if (-not $webProcess.WaitForExit(15000)) { throw 'WebAPI did not exit after CTRL_BREAK_EVENT.' }
    $webProcess.Refresh()
    if (-not $webProcess.HasExited) { throw 'WebAPI process is still running after CTRL_BREAK_EVENT.' }
    if (Get-Process -Id $webProcess.Id -ErrorAction SilentlyContinue) { throw 'WebAPI process remains listed after graceful shutdown.' }

    $lockOwner = (Get-Content -LiteralPath $lockPath -Raw).Trim()
    if ($lockOwner -ne [string]$webProcess.Id) { throw "Instance lock owner mismatch: $lockOwner" }
    try {
        $response = $client.GetAsync($healthUrl).GetAwaiter().GetResult()
        throw "Health endpoint remained reachable after shutdown (HTTP $([int]$response.StatusCode))."
    } catch [System.Net.Http.HttpRequestException] {
        # Expected: Kestrel has closed the listener after graceful shutdown.
    }

    $success = $true
    Write-Output "Windows smoke passed: win-x64 self-contained EXE, first-run data paths, $healthUrl, CTRL+BREAK graceful shutdown."
    Write-Output "Windows: $([Environment]::OSVersion.VersionString); process exited cleanly after CTRL+BREAK."
} finally {
    if ($nativeHandlesOpen -and $null -ne $processInfo) {
        if ($processInfo.hThread -ne [IntPtr]::Zero) { [void][WebApiWindowsSmokeNative]::CloseHandle($processInfo.hThread) }
        if ($processInfo.hProcess -ne [IntPtr]::Zero) { [void][WebApiWindowsSmokeNative]::CloseHandle($processInfo.hProcess) }
    }
    if ($null -ne $webProcess) {
        try {
            if (-not $webProcess.HasExited) {
                [void][WebApiWindowsSmokeNative]::GenerateConsoleCtrlEvent(1, [uint32]$webProcess.Id)
                if (-not $webProcess.WaitForExit(5000)) { Stop-Process -Id $webProcess.Id -Force }
            }
        } catch {
            try { Stop-Process -Id $webProcess.Id -Force -ErrorAction SilentlyContinue } catch { }
        }
        $webProcess.Dispose()
    }
    if ($null -ne $client) { $client.Dispose() }
    foreach ($name in $environmentNames) {
        [Environment]::SetEnvironmentVariable($name, $oldEnvironment[$name], 'Process')
    }
    if ($success) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    } else {
        Write-Warning "Smoke test failed; isolated files were retained at $testRoot"
    }
}
