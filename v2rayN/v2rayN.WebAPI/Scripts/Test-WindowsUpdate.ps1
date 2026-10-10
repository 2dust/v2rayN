param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$PreviousPublishDirectory,
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$CandidatePublishDirectory
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$previousRoot = (Resolve-Path -LiteralPath $PreviousPublishDirectory).Path
$candidateRoot = (Resolve-Path -LiteralPath $CandidatePublishDirectory).Path
$executableName = 'v2rayN.WebAPI.exe'
$previousExe = Join-Path $previousRoot $executableName
$candidateExe = Join-Path $candidateRoot $executableName
$previousIdentityPath = Join-Path $previousRoot 'v2rayN.WebAPI.build.json'
$candidateIdentityPath = Join-Path $candidateRoot 'v2rayN.WebAPI.build.json'
foreach ($path in @($previousExe, $candidateExe, $previousIdentityPath, $candidateIdentityPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing Windows update fixture file: $path" }
}

$previousIdentity = Get-Content -LiteralPath $previousIdentityPath -Raw | ConvertFrom-Json
$candidateIdentity = Get-Content -LiteralPath $candidateIdentityPath -Raw | ConvertFrom-Json
if ($previousIdentity.rid -ne 'win-x64' -or $candidateIdentity.rid -ne 'win-x64') { throw 'Update fixtures must target win-x64.' }
if ($previousIdentity.version -eq $candidateIdentity.version) { throw 'Update fixture versions must differ.' }
if (-not [Environment]::Is64BitOperatingSystem) { throw 'Windows WebAPI update requires 64-bit Windows.' }

if (-not ('WebApiWindowsUpdateNative' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class WebApiWindowsUpdateNative
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

$testRoot = Join-Path $env:TEMP ('v2rayN.WebAPI-update-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$environmentNames = @(
    'V2RAYN_DATA_HOME', 'V2RAYN_LOCAL_APPLICATION_DATA_V2', 'V2RAYN_WEB_API_KEY',
    'V2RAYN_WEB_AUTOSTART', 'ASPNETCORE_URLS', 'XDG_DATA_HOME', 'DOTNET_BUNDLE_EXTRACT_BASE_DIR',
    'INVOCATION_ID', 'JOURNAL_STREAM', 'NOTIFY_SOCKET', 'DOTNET_RUNNING_IN_CONTAINER', 'container'
)
$oldEnvironment = @{}
foreach ($name in $environmentNames) {
    $oldEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$success = $false

function Set-TestEnvironment([string]$Url, [string]$Key, [string]$BundleDirectory) {
    foreach ($name in @('V2RAYN_DATA_HOME','XDG_DATA_HOME','INVOCATION_ID','JOURNAL_STREAM','NOTIFY_SOCKET','DOTNET_RUNNING_IN_CONTAINER','container')) {
        [Environment]::SetEnvironmentVariable($name, $null, 'Process')
    }
    [Environment]::SetEnvironmentVariable('V2RAYN_LOCAL_APPLICATION_DATA_V2', '0', 'Process')
    [Environment]::SetEnvironmentVariable('V2RAYN_WEB_API_KEY', $Key, 'Process')
    [Environment]::SetEnvironmentVariable('V2RAYN_WEB_AUTOSTART', 'false', 'Process')
    [Environment]::SetEnvironmentVariable('ASPNETCORE_URLS', $Url, 'Process')
    [Environment]::SetEnvironmentVariable('DOTNET_BUNDLE_EXTRACT_BASE_DIR', $BundleDirectory, 'Process')
}

function Restore-TestEnvironment {
    foreach ($name in $environmentNames) {
        [Environment]::SetEnvironmentVariable($name, $oldEnvironment[$name], 'Process')
    }
}

function Start-TestOwner([string]$InstallDirectory, [string]$Url, [string]$Key, [string]$BundleDirectory) {
    Set-TestEnvironment $Url $Key $BundleDirectory
    try {
        $exe = Join-Path $InstallDirectory $executableName
        $commandLine = '"{0}" --foreground --no-open --urls "{1}"' -f $exe, $Url
        $startupInfo = [WebApiWindowsUpdateNative+STARTUPINFO]::new()
        $startupInfo.cb = [Runtime.InteropServices.Marshal]::SizeOf([type][WebApiWindowsUpdateNative+STARTUPINFO])
        $processInfo = [WebApiWindowsUpdateNative+PROCESS_INFORMATION]::new()
        $created = [WebApiWindowsUpdateNative]::CreateProcess(
            $exe, [System.Text.StringBuilder]::new($commandLine), [IntPtr]::Zero, [IntPtr]::Zero,
            $true, [uint32]0x00000200, [IntPtr]::Zero, $InstallDirectory,
            [ref]$startupInfo, [ref]$processInfo)
        if (-not $created) {
            $code = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
            throw "Could not start the previous Windows WebAPI (Win32 error $code)."
        }
        [void][WebApiWindowsUpdateNative]::CloseHandle($processInfo.hThread)
        [void][WebApiWindowsUpdateNative]::CloseHandle($processInfo.hProcess)
        return [Diagnostics.Process]::GetProcessById([int]$processInfo.dwProcessId)
    } finally {
        Restore-TestEnvironment
    }
}

function Start-TestHelper([string]$HelperPath, [string]$PlanPath, [string]$InstallDirectory, [string]$Url, [string]$Key, [string]$BundleDirectory) {
    Set-TestEnvironment $Url $Key $BundleDirectory
    try {
        $startInfo = [Diagnostics.ProcessStartInfo]::new()
        $startInfo.FileName = $HelperPath
        $startInfo.WorkingDirectory = $InstallDirectory
        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $true
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        $startInfo.Arguments = '"--apply-WebAPI-update" "{0}"' -f $PlanPath.Replace('"', '\"')
        $process = [Diagnostics.Process]::Start($startInfo)
        return [pscustomobject]@{
            Process = $process
            StandardOutput = $process.StandardOutput.ReadToEndAsync()
            StandardError = $process.StandardError.ReadToEndAsync()
        }
    } finally {
        Restore-TestEnvironment
    }
}

function Wait-TestHealth([string]$Url, [string]$ExpectedVersion, [int]$TimeoutSeconds = 35) {
    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.UseProxy = $false
    $client = [System.Net.Http.HttpClient]::new($handler)
    try {
        $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
        $lastError = $null
        while ([DateTime]::UtcNow -lt $deadline) {
            try {
                $response = $client.GetAsync("$Url/api/health").GetAwaiter().GetResult()
                $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                if ([int]$response.StatusCode -eq 200) {
                    $version = $response.Headers.GetValues('X-v2rayn-WebAPI-version') | Select-Object -First 1
                    $ownerId = $response.Headers.GetValues('X-v2rayn-WebAPI-instance-pid') | Select-Object -First 1
                    if ($version -eq $ExpectedVersion -and $ownerId) {
                        return [pscustomobject]@{ Version = $version; ProcessId = [int]$ownerId; Body = $body }
                    }
                    $lastError = "unexpected version ${version}: $body"
                } else {
                    $lastError = "HTTP $([int]$response.StatusCode): $body"
                }
                $response.Dispose()
            } catch {
                $lastError = $_.Exception.Message
            }
            Start-Sleep -Milliseconds 250
        }
        throw "Windows WebAPI health did not reach version $ExpectedVersion. Last error: $lastError"
    } finally {
        $client.Dispose()
    }
}

function Stop-TestOwner([Diagnostics.Process]$Process) {
    if ($null -eq $Process -or $Process.HasExited) { return }
    if (-not [WebApiWindowsUpdateNative]::GenerateConsoleCtrlEvent(1, [uint32]$Process.Id)) {
        $code = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
        throw "Could not send graceful CTRL_BREAK_EVENT (Win32 error $code)."
    }
    if (-not $Process.WaitForExit(15000)) { throw "Windows WebAPI process $($Process.Id) did not exit gracefully." }
}

function Write-TestJson([string]$Path, $Value) {
    $json = $Value | ConvertTo-Json -Depth 8 -Compress
    [IO.File]::WriteAllText($Path, $json, [Text.UTF8Encoding]::new($false))
}

function Invoke-UpdateScenario([bool]$ExpectRollback) {
    $caseRoot = Join-Path $testRoot ([Guid]::NewGuid().ToString('N'))
    $install = Join-Path $caseRoot 'install'
    $candidate = Join-Path $caseRoot ('.v2rayn-WebAPI-candidate-' + [Guid]::NewGuid().ToString('N'))
    $backup = Join-Path $caseRoot ('.v2rayn-WebAPI-backup-' + [Guid]::NewGuid().ToString('N'))
    $core = Join-Path $install 'bin'
    $gui = Join-Path $install 'guiConfigs'
    $temps = Join-Path $install 'guiTemps'
    New-Item -ItemType Directory -Path $install,$candidate,$gui,$temps -Force | Out-Null
    Copy-Item -LiteralPath $previousExe -Destination (Join-Path $install $executableName)
    Copy-Item -LiteralPath $previousIdentityPath -Destination (Join-Path $install 'v2rayN.WebAPI.build.json')
    $previousBin = Join-Path $previousRoot 'bin'
    if (-not (Test-Path -LiteralPath $previousBin -PathType Container)) { throw "Previous update fixture is missing Core data: $previousBin" }
    Copy-Item -LiteralPath $previousBin -Destination $install -Recurse -Force
    Copy-Item -LiteralPath $candidateExe -Destination (Join-Path $candidate $executableName)
    Copy-Item -LiteralPath $candidateIdentityPath -Destination (Join-Path $candidate 'v2rayN.WebAPI.build.json')
    $previousAppIdentity = Get-Content -LiteralPath (Join-Path $install 'v2rayN.WebAPI.build.json') -Raw | ConvertFrom-Json
    $candidateAppIdentity = Get-Content -LiteralPath (Join-Path $candidate 'v2rayN.WebAPI.build.json') -Raw | ConvertFrom-Json
    $expectedVersion = if ($ExpectRollback) { '0.0.999' } else { $candidateAppIdentity.version }
    if ($ExpectRollback) {
        $candidateAppIdentity.version = $expectedVersion
        Write-TestJson (Join-Path $candidate 'v2rayN.WebAPI.build.json') $candidateAppIdentity
    }

    $key = 'Windows-update-' + [Guid]::NewGuid().ToString('N')
    $envPath = Join-Path $install '.env'
    [IO.File]::WriteAllText($envPath, "V2RAYN_WEB_API_KEY=$key`n", [Text.UTF8Encoding]::new($false))
    $userConfigMarker = Join-Path $gui 'web-update-test-marker.dat'
    [IO.File]::WriteAllText($userConfigMarker, 'USER_CONFIG_KEEP')
    [IO.File]::WriteAllText((Join-Path $core 'web-update-test-marker.dat'), 'CORE_FILES_KEEP')
    $envHash = (Get-FileHash -LiteralPath $envPath -Algorithm SHA256).Hash
    $configHash = (Get-FileHash -LiteralPath $userConfigMarker -Algorithm SHA256).Hash
    $coreHash = (Get-FileHash -LiteralPath (Join-Path $core 'web-update-test-marker.dat') -Algorithm SHA256).Hash

    $portListener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $portListener.Start()
    $port = ([System.Net.IPEndPoint]$portListener.LocalEndpoint).Port
    $portListener.Stop()
    $url = "http://127.0.0.1:$port"
    $bundleRoot = Join-Path $caseRoot 'bundle'
    New-Item -ItemType Directory -Path $bundleRoot -Force | Out-Null
    $owner = $null
    $helper = $null
    $updatedProcessId = $null
    try {
        $owner = Start-TestOwner $install $url $key $bundleRoot
        $previousHealth = Wait-TestHealth $url $previousAppIdentity.version
        if ($previousHealth.ProcessId -ne $owner.Id) { throw 'Previous Windows WebAPI lock/health PID mismatch.' }

        $runtimeIntentPath = Join-Path $temps 'WebAPI-update-runtime-state.json'
        $progressPath = Join-Path $temps 'WebAPI-update-progress.json'
        $planPath = Join-Path $temps 'windows-update-plan.json'
        Write-TestJson $runtimeIntentPath @{ wasRunning = $false; preferredProfileId = $null; reason = 'Windows update smoke' }
        Write-TestJson $planPath @{
            installDirectory = $install
            candidateDirectory = $candidate
            backupDirectory = $backup
            instanceLockPath = (Join-Path $install 'v2rayN.WebAPI.instance.lock')
            healthUri = "$url/api/health"
            hostArguments = @('--urls', $url)
            expectedVersion = $expectedVersion
            expectedCommit = $candidateAppIdentity.commit
            rid = 'win-x64'
            previousVersion = $previousAppIdentity.version
            runtimeIntentPath = $runtimeIntentPath
            progressPath = $progressPath
            coreWasRunning = $false
        }

        $helperPath = Join-Path $install ('.v2rayn-WebAPI-update-helper-' + [Guid]::NewGuid().ToString('N') + '.exe')
        Copy-Item -LiteralPath (Join-Path $install $executableName) -Destination $helperPath
        $helperResult = Start-TestHelper $helperPath $planPath $install $url $key $bundleRoot
        $helper = $helperResult.Process
        Stop-TestOwner $owner
        $observedHealth = [System.Collections.Generic.List[string]]::new()
        $healthHandler = [System.Net.Http.HttpClientHandler]::new()
        $healthHandler.UseProxy = $false
        $healthClient = [System.Net.Http.HttpClient]::new($healthHandler)
        try {
            $helperDeadline = [DateTime]::UtcNow.AddSeconds(180)
            while (-not $helper.HasExited -and [DateTime]::UtcNow -lt $helperDeadline) {
                try {
                    $response = $healthClient.GetAsync("$url/api/health").GetAwaiter().GetResult()
                    $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                    if ([int]$response.StatusCode -eq 200) {
                        $version = $response.Headers.GetValues('X-v2rayn-WebAPI-version') | Select-Object -First 1
                        $instanceOwner = $response.Headers.GetValues('X-v2rayn-WebAPI-instance-pid') | Select-Object -First 1
                        $coreState = $response.Headers.GetValues('X-v2rayn-WebAPI-core-state') | Select-Object -First 1
                        $corePids = $response.Headers.GetValues('X-v2rayn-WebAPI-core-process-ids') | Select-Object -First 1
                        $snapshot = "version=$version; pid=$instanceOwner; coreState=$coreState; corePids=$corePids; runtimeIntent=$(Test-Path -LiteralPath $runtimeIntentPath)"
                        if ($observedHealth.Count -eq 0 -or $observedHealth[$observedHealth.Count - 1] -ne $snapshot) {
                            if ($observedHealth.Count -lt 20) { $observedHealth.Add($snapshot) }
                        }
                    }
                    $response.Dispose()
                } catch { }
                Start-Sleep -Milliseconds 200
            }
            if (-not $helper.HasExited) { throw 'Windows native update helper timed out.' }
        } finally {
            $healthClient.Dispose()
        }
        $expectedExit = if ($ExpectRollback) { 1 } else { 0 }
        $helperOutput = $helperResult.StandardOutput.GetAwaiter().GetResult()
        $helperError = $helperResult.StandardError.GetAwaiter().GetResult()
        if ($helper.ExitCode -ne $expectedExit) {
            $progressDump = if (Test-Path -LiteralPath $progressPath) { Get-Content -LiteralPath $progressPath -Raw } else { '<missing>' }
            throw "Windows update helper exit $($helper.ExitCode), expected $expectedExit.`nhealth observations:`n$($observedHealth -join "`n")`nprogress:`n$progressDump`n$helperOutput`n$helperError"
        }

        $expectedHealthVersion = if ($ExpectRollback) { $previousAppIdentity.version } else { $candidateAppIdentity.version }
        $health = Wait-TestHealth $url $expectedHealthVersion
        $updatedProcessId = $health.ProcessId
        $progress = Get-Content -LiteralPath $progressPath -Raw | ConvertFrom-Json
        if (-not $progress.isComplete -or $progress.success -eq $ExpectRollback) { throw "Unexpected Windows update progress: $($progress | ConvertTo-Json -Compress)" }
        if ($ExpectRollback -and -not $progress.rollbackSucceeded) { throw 'Windows update rollback was not confirmed.' }

        $expectedExe = if ($ExpectRollback) { $previousExe } else { $candidateExe }
        if ((Get-FileHash -LiteralPath (Join-Path $install $executableName) -Algorithm SHA256).Hash -ne
            (Get-FileHash -LiteralPath $expectedExe -Algorithm SHA256).Hash) { throw 'Windows update executable content is incorrect.' }
        $installedIdentity = Get-Content -LiteralPath (Join-Path $install 'v2rayN.WebAPI.build.json') -Raw | ConvertFrom-Json
        if ($installedIdentity.version -ne $expectedHealthVersion) { throw 'Windows update build identity is incorrect.' }
        if ((Get-FileHash -LiteralPath $envPath -Algorithm SHA256).Hash -ne $envHash) { throw 'Windows update changed .env.' }
        if ((Get-FileHash -LiteralPath $userConfigMarker -Algorithm SHA256).Hash -ne $configHash) { throw 'Windows update changed user config.' }
        if ((Get-FileHash -LiteralPath (Join-Path $core 'web-update-test-marker.dat') -Algorithm SHA256).Hash -ne $coreHash) { throw 'Windows update changed Core files.' }
        if (Test-Path -LiteralPath $backup) { throw 'Windows update did not clean its backup directory.' }
        if (Test-Path -LiteralPath $candidate) { throw 'Windows update did not clean its candidate directory.' }
        $helperDeadline = [DateTime]::UtcNow.AddSeconds(10)
        while ((Test-Path -LiteralPath $helperPath) -and [DateTime]::UtcNow -lt $helperDeadline) { Start-Sleep -Milliseconds 200 }
        if (Test-Path -LiteralPath $helperPath) { throw 'Windows update helper copy was not cleaned.' }
        if ($ExpectRollback) { Write-Output 'Windows native update rollback passed.' }
        else { Write-Output 'Windows native update replacement passed.' }
        return $updatedProcessId
    } finally {
        foreach ($processId in @($updatedProcessId, $(if ($owner) { $owner.Id } else { $null }))) {
            if ($processId) { Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue }
        }
        if ($helper) { $helper.Dispose() }
        if ($owner) { $owner.Dispose() }
    }
}

try {
    Invoke-UpdateScenario $false | Out-Null
    Invoke-UpdateScenario $true | Out-Null
    $success = $true
    Write-Output 'Windows native self-update replacement and rollback smoke passed.'
} finally {
    Restore-TestEnvironment
    if ($success) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    } else {
        Write-Warning "Windows update test failed; isolated fixture retained at $testRoot"
    }
}
