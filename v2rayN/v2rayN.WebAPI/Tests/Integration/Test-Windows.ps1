param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('startup', 'update', 'restore')]
    [string]$Mode,
    [ValidateNotNullOrEmpty()]
    [string]$PublishDirectory,
    [ValidateNotNullOrEmpty()]
    [string]$PreviousPublishDirectory,
    [ValidateNotNullOrEmpty()]
    [string]$CandidatePublishDirectory
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

if ($Mode -in @('startup', 'restore')) {
    if (-not $PublishDirectory) { throw "$Mode mode requires -PublishDirectory." }
    if ($Mode -eq 'restore') {
        $xray = Join-Path $PublishDirectory 'bin\xray\xray.exe'
        if (-not (Test-Path -LiteralPath $xray -PathType Leaf)) {
            throw "restore mode requires the bundled Xray executable: $xray"
        }
    }
} elseif ($Mode -eq 'update') {
    if (-not $PreviousPublishDirectory -or -not $CandidatePublishDirectory) {
        throw 'update mode requires -PreviousPublishDirectory and -CandidatePublishDirectory.'
    }
}

$executableName = 'v2rayN.WebAPI.exe'
if (-not [Environment]::Is64BitOperatingSystem) { throw 'v2rayN.WebAPI Windows smoke requires 64-bit Windows.' }

# Shared native process control: the app must run in its own process group so the smoke can
# send a graceful CTRL_BREAK_EVENT instead of killing it.
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

$testRoot = Join-Path $env:TEMP ('v2rayN.WebAPI-windows-' + $Mode + '-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null

# The union covers both modes so the finally block restores exactly the original state.
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

function Clear-TestEnvironment {
    foreach ($name in $environmentNames) {
        [Environment]::SetEnvironmentVariable($name, $null, 'Process')
    }
}

function Set-UpdateEnvironment([string]$Url, [string]$Key, [string]$BundleDirectory) {
    Clear-TestEnvironment
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

function New-NoProxyHttpClient {
    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.UseProxy = $false
    return [System.Net.Http.HttpClient]::new($handler)
}

function Get-FreeLoopbackPort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    $listener.Stop()
    return $port
}

function Start-NativeTestProcess([string]$Application, [string]$Arguments, [string]$WorkingDirectory) {
    $commandLine = '"{0}" {1}' -f $Application, $Arguments
    $startupInfo = [WebApiWindowsSmokeNative+STARTUPINFO]::new()
    $startupInfo.cb = [Runtime.InteropServices.Marshal]::SizeOf([type][WebApiWindowsSmokeNative+STARTUPINFO])
    $processInfo = [WebApiWindowsSmokeNative+PROCESS_INFORMATION]::new()
    $created = [WebApiWindowsSmokeNative]::CreateProcess(
        $Application, [System.Text.StringBuilder]::new($commandLine), [IntPtr]::Zero, [IntPtr]::Zero,
        $true, [uint32]0x00000200, [IntPtr]::Zero, $WorkingDirectory,
        [ref]$startupInfo, [ref]$processInfo)
    if (-not $created) {
        $code = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
        throw "Could not start $Application (Win32 error $code)."
    }
    try {
        return [Diagnostics.Process]::GetProcessById([int]$processInfo.dwProcessId)
    } finally {
        if ($processInfo.hThread -ne [IntPtr]::Zero) { [void][WebApiWindowsSmokeNative]::CloseHandle($processInfo.hThread) }
        if ($processInfo.hProcess -ne [IntPtr]::Zero) { [void][WebApiWindowsSmokeNative]::CloseHandle($processInfo.hProcess) }
    }
}

function Stop-NativeTestProcess([Diagnostics.Process]$Process, [int]$TimeoutMilliseconds = 15000) {
    if ($null -eq $Process -or $Process.HasExited) { return }
    if (-not [WebApiWindowsSmokeNative]::GenerateConsoleCtrlEvent(1, [uint32]$Process.Id)) {
        $code = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
        throw "Could not send graceful CTRL_BREAK_EVENT (Win32 error $code)."
    }
    if (-not $Process.WaitForExit($TimeoutMilliseconds)) {
        throw "Windows WebAPI process $($Process.Id) did not exit gracefully."
    }
}

function Stop-TestProcessAndWait([int]$ProcessId, [int]$TimeoutMilliseconds = 15000) {
    if (-not $ProcessId -or -not (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue)) { return }
    Stop-Process -Id $ProcessId -Force -ErrorAction SilentlyContinue
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (-not (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue)) { return }
        Start-Sleep -Milliseconds 100
    }
    if (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) {
        throw "Windows process $ProcessId did not exit during smoke cleanup."
    }
}

function Wait-TestHealth {
    param(
        [string]$Url,
        [string]$ExpectedVersion = '',
        [Diagnostics.Process]$Process,
        [int]$TimeoutSeconds = 35,
        [int]$PreviousProcessId = 0
    )
    $client = New-NoProxyHttpClient
    try {
        $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
        $lastError = $null
        while ([DateTime]::UtcNow -lt $deadline) {
            if ($null -ne $Process -and $Process.HasExited) {
                throw "WebAPI exited during startup (code $($Process.ExitCode))."
            }
            try {
                $response = $client.GetAsync("$Url/api/health").GetAwaiter().GetResult()
                $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                if ([int]$response.StatusCode -eq 200) {
                    # The original startup smoke parsed the body and required status "ok";
                    # keep that assertion for both modes instead of trusting the 200 alone.
                    $healthBody = $body | ConvertFrom-Json
                    if ($healthBody.status -ne 'ok') { throw "Unexpected health response: $body" }
                    if (-not $ExpectedVersion) {
                        return [pscustomobject]@{ Version = $null; ProcessId = $null; Body = $body }
                    }
                    $version = $response.Headers.GetValues('X-v2rayn-WebAPI-version') | Select-Object -First 1
                    $ownerId = $response.Headers.GetValues('X-v2rayn-WebAPI-instance-pid') | Select-Object -First 1
                    $isPreviousProcess = $PreviousProcessId -gt 0 -and $ownerId -and [int]$ownerId -eq $PreviousProcessId
                    if ($version -eq $ExpectedVersion -and $ownerId -and -not $isPreviousProcess) {
                        return [pscustomobject]@{ Version = $version; ProcessId = [int]$ownerId; Body = $body }
                    }
                    if ($isPreviousProcess) {
                        $lastError = "the previous process $PreviousProcessId is still serving health"
                    } else {
                        $lastError = "unexpected version ${version}: $body"
                    }
                } else {
                    $lastError = "HTTP $([int]$response.StatusCode): $body"
                }
                $response.Dispose()
            } catch {
                $lastError = $_.Exception.Message
            }
            Start-Sleep -Milliseconds 250
        }
        if ($ExpectedVersion) {
            throw "Windows WebAPI health did not reach version $ExpectedVersion. Last error: $lastError"
        }
        throw "Health check failed at $Url/api/health. Last error: $lastError"
    } finally {
        $client.Dispose()
    }
}

function Write-TestJson([string]$Path, $Value) {
    $json = $Value | ConvertTo-Json -Depth 8 -Compress
    [IO.File]::WriteAllText($Path, $json, [Text.UTF8Encoding]::new($false))
}

function Invoke-WebApiJson {
    param(
        [System.Net.Http.HttpClient]$Client,
        [string]$Url,
        [string]$Path,
        [string]$Method = 'GET',
        [string]$Token,
        [object]$Body,
        [string]$UploadPath
    )

    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($Method), "$Url$Path")
    if ($Token) {
        $request.Headers.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $Token)
    }
    if ($UploadPath) {
        $multipart = [System.Net.Http.MultipartFormDataContent]::new()
        $fileContent = [System.Net.Http.ByteArrayContent]::new([IO.File]::ReadAllBytes($UploadPath))
        $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::new('application/zip')
        $multipart.Add($fileContent, 'file', [IO.Path]::GetFileName($UploadPath))
        $request.Content = $multipart
    } elseif ($null -ne $Body) {
        $json = $Body | ConvertTo-Json -Depth 16 -Compress
        $request.Content = [System.Net.Http.StringContent]::new($json, [Text.Encoding]::UTF8, 'application/json')
    }

    try {
        $response = $Client.SendAsync($request).GetAwaiter().GetResult()
        try {
            $text = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            $parsed = if ($text) { $text | ConvertFrom-Json } else { $null }
            return [pscustomobject]@{ StatusCode = [int]$response.StatusCode; Json = $parsed; Raw = $text }
        } finally {
            $response.Dispose()
        }
    } finally {
        $request.Dispose()
    }
}

function Get-WebApiSessionToken {
    param([System.Net.Http.HttpClient]$Client, [string]$Url, [string]$Key)
    $login = Invoke-WebApiJson -Client $Client -Url $Url -Path '/api/auth/login' -Method 'POST' -Body @{ key = $Key }
    if (-not $login.Json.success -or -not $login.Json.data.token) {
        throw "Windows restore smoke login failed: $($login.Raw)"
    }
    return [string]$login.Json.data.token
}

function Download-TestBackup {
    param([System.Net.Http.HttpClient]$Client, [string]$Url, [string]$Token, [string]$Path)
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, "$Url/api/backup/download")
    $request.Headers.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $Token)
    try {
        $response = $Client.SendAsync($request).GetAwaiter().GetResult()
        try {
            if (-not $response.IsSuccessStatusCode) {
                throw "Backup download failed with HTTP $([int]$response.StatusCode)."
            }
            [IO.File]::WriteAllBytes($Path, $response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult())
        } finally {
            $response.Dispose()
        }
    } finally {
        $request.Dispose()
    }
}

function Add-RestoreMarkerToBackup([string]$ArchivePath) {
    $archive = [System.IO.Compression.ZipFile]::Open($ArchivePath, [System.IO.Compression.ZipArchiveMode]::Update)
    try {
        if ($null -ne $archive.GetEntry('guiConfigs/restore-smoke-marker.txt')) {
            throw 'The generated backup unexpectedly contains the restore smoke marker already.'
        }
        $entry = $archive.CreateEntry('guiConfigs/restore-smoke-marker.txt')
        $stream = $entry.Open()
        try {
            $writer = [IO.StreamWriter]::new($stream, [Text.UTF8Encoding]::new($false))
            try { $writer.Write('RESTORE_CONFIG_APPLIED') } finally { $writer.Dispose() }
        } finally {
            $stream.Dispose()
        }
    } finally {
        $archive.Dispose()
    }
}

function Invoke-RestoreScenario([bool]$CoreWasRunning) {
    $packageRoot = (Resolve-Path -LiteralPath $PublishDirectory).Path
    $scenario = if ($CoreWasRunning) { 'running-core' } else { 'stopped-core' }
    $caseRoot = Join-Path $testRoot $scenario
    $install = Join-Path $caseRoot 'install'
    New-Item -ItemType Directory -Path $install -Force | Out-Null
    Get-ChildItem -LiteralPath $packageRoot -Force | Where-Object { $_.Name -ne '.env' } | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $install -Recurse -Force
    }

    $identity = Get-Content -LiteralPath (Join-Path $install 'v2rayN.WebAPI.build.json') -Raw | ConvertFrom-Json
    if ($identity.rid -ne 'win-x64') { throw 'Windows restore smoke must run against the win-x64 package.' }
    $guiDirectory = Join-Path $install 'guiConfigs'
    $markerPath = Join-Path $guiDirectory 'restore-smoke-marker.txt'
    $key = 'Windows-restore-' + [Guid]::NewGuid().ToString('N')
    $url = "http://127.0.0.1:$(Get-FreeLoopbackPort)"
    $bundleRoot = Join-Path $caseRoot 'bundle'
    New-Item -ItemType Directory -Path $bundleRoot -Force | Out-Null
    $archivePath = Join-Path $caseRoot 'restore.zip'
    $owner = $null
    $replacementProcessId = $null
    $client = New-NoProxyHttpClient
    $token = $null
    $success = $false
    try {
        Set-UpdateEnvironment $url $key $bundleRoot
        try {
            $executable = Join-Path $install $executableName
            $owner = Start-NativeTestProcess $executable ('--foreground --no-open --urls "{0}"' -f $url) $install
        } finally {
            Restore-TestEnvironment
        }

        $started = Wait-TestHealth -Url $url -ExpectedVersion $identity.version -Process $owner
        if ($started.ProcessId -ne $owner.Id) { throw 'Initial Windows restore smoke lock/health PID mismatch.' }
        $token = Get-WebApiSessionToken -Client $client -Url $url -Key $key

        if ($CoreWasRunning) {
            $profile = @{
                configType = 'vless'
                remarks = 'Windows backup restore smoke'
                address = '127.0.0.1'
                port = 65535
                password = [Guid]::NewGuid().ToString()
                network = 'tcp'
                streamSecurity = ''
            }
            $saved = Invoke-WebApiJson -Client $client -Url $url -Path '/api/profiles' -Method 'POST' -Token $token -Body $profile
            if (-not $saved.Json.success -or -not $saved.Json.data.profileId) {
                throw "Could not create the Windows Core restore smoke profile: $($saved.Raw)"
            }
            $profileId = [string]$saved.Json.data.profileId
            $selected = Invoke-WebApiJson -Client $client -Url $url -Path "/api/profiles/$profileId/select" -Method 'POST' -Token $token
            if (-not $selected.Json.success) { throw "Could not start the Windows restore smoke Core: $($selected.Raw)" }
        }

        $beforeStatus = Invoke-WebApiJson -Client $client -Url $url -Path '/api/status' -Token $token
        if ($CoreWasRunning) {
            if (-not $beforeStatus.Json.data.coreRunning -or $beforeStatus.Json.data.runtimeState -ne 'running') {
                throw "Windows restore smoke did not start Core before backup: $($beforeStatus.Raw)"
            }
        } elseif ($beforeStatus.Json.data.coreRunning -or $beforeStatus.Json.data.runtimeState -ne 'stopped') {
            throw "Windows restore smoke expected Core to remain stopped before backup: $($beforeStatus.Raw)"
        }

        Download-TestBackup -Client $client -Url $url -Token $token -Path $archivePath
        Add-RestoreMarkerToBackup $archivePath
        if (Test-Path -LiteralPath $markerPath) { throw 'Restore marker exists before the Windows backup restore.' }

        $restore = Invoke-WebApiJson -Client $client -Url $url -Path '/api/backup/restore' -Method 'POST' -Token $token -UploadPath $archivePath
        if ($restore.StatusCode -ne 202 -or -not $restore.Json.success) {
            throw "Windows backup restore was not accepted (HTTP $($restore.StatusCode)): $($restore.Raw)"
        }

        $health = Wait-TestHealth -Url $url -ExpectedVersion $identity.version -TimeoutSeconds 90 -PreviousProcessId $owner.Id
        $replacementProcessId = $health.ProcessId
        if ($replacementProcessId -eq $owner.Id) { throw 'Windows backup restore reconnected to the old WebAPI process.' }
        if (-not (Get-Process -Id $replacementProcessId -ErrorAction SilentlyContinue)) {
            throw "Windows restored WebAPI process $replacementProcessId is not running."
        }
        if (-not $owner.WaitForExit(20000)) { throw 'The old Windows WebAPI process did not exit after verified restore handoff.' }
        if (-not (Test-Path -LiteralPath $markerPath -PathType Leaf)) { throw 'The restored Windows config marker was not installed.' }
        if ((Get-Content -LiteralPath $markerPath -Raw) -ne 'RESTORE_CONFIG_APPLIED') { throw 'The restored Windows config marker content is incorrect.' }

        # Backend restart invalidates in-memory sessions; reconnect with the retained local key.
        $token = Get-WebApiSessionToken -Client $client -Url $url -Key $key
        $afterStatus = Invoke-WebApiJson -Client $client -Url $url -Path '/api/status' -Token $token
        $coreIds = @($afterStatus.Json.data.coreProcessIds | Where-Object { $_ })
        if ($CoreWasRunning) {
            if (-not $afterStatus.Json.data.coreRunning -or $afterStatus.Json.data.runtimeState -ne 'running' -or $coreIds.Count -eq 0) {
                throw "Windows restore did not recover the previously running Core: $($afterStatus.Raw)"
            }
            Write-Output 'Windows backup restore passed (new healthy Web PID, restored config, previously running Core restarted).'
        } else {
            if ($afterStatus.Json.data.coreRunning -or $afterStatus.Json.data.runtimeState -ne 'stopped' -or $coreIds.Count -ne 0) {
                throw "Windows restore changed the previously stopped Core intent: $($afterStatus.Raw)"
            }
            Write-Output 'Windows backup restore passed (new healthy Web PID, restored config, previously stopped Core remains stopped).'
        }
        $success = $true
    } finally {
        $cleanupError = $null
        if ($null -ne $client -and $replacementProcessId -and (Get-Process -Id $replacementProcessId -ErrorAction SilentlyContinue)) {
            try {
                if ($token) { [void](Invoke-WebApiJson -Client $client -Url $url -Path '/api/core/stop' -Method 'POST' -Token $token) }
            } catch { }
            try { Stop-Process -Id $replacementProcessId -Force -ErrorAction SilentlyContinue } catch { }
            for ($attempt = 0; $attempt -lt 100; $attempt++) {
                if (-not (Get-Process -Id $replacementProcessId -ErrorAction SilentlyContinue)) { break }
                Start-Sleep -Milliseconds 100
            }
            if (Get-Process -Id $replacementProcessId -ErrorAction SilentlyContinue) {
                $cleanupError = "Restored Windows WebAPI process $replacementProcessId did not exit during cleanup."
            }
        }
        if ($null -ne $owner) {
            try {
                if (-not $owner.HasExited) {
                    try { [void][WebApiWindowsSmokeNative]::GenerateConsoleCtrlEvent(1, [uint32]$owner.Id) } catch { }
                    if (-not $owner.WaitForExit(5000)) { Stop-Process -Id $owner.Id -Force -ErrorAction SilentlyContinue }
                    if (-not $owner.WaitForExit(10000)) {
                        if ($null -eq $cleanupError) { $cleanupError = "Old Windows WebAPI process $($owner.Id) did not exit during cleanup." }
                    }
                }
            } catch { try { Stop-Process -Id $owner.Id -Force -ErrorAction SilentlyContinue } catch { } }
            $owner.Dispose()
        }
        if ($null -ne $client) { $client.Dispose() }
        if ($cleanupError) { throw $cleanupError }
    }
}

function Invoke-RestoreSmoke {
    Invoke-RestoreScenario $false
    Invoke-RestoreScenario $true
    Write-Output 'Windows native backup restore replacement and Core-intent smoke passed.'
}

# First startup, health endpoint, first-run data paths and graceful shutdown.
function Invoke-StartupSmoke {
    $packageRoot = (Resolve-Path -LiteralPath $PublishDirectory).Path
    $sourceExe = Join-Path $packageRoot $executableName
    if (-not (Test-Path -LiteralPath $sourceExe -PathType Leaf)) { throw "Missing executable: $sourceExe" }

    $runtimeDir = Join-Path $testRoot 'runtime'
    New-Item -ItemType Directory -Path $runtimeDir -Force | Out-Null
    Get-ChildItem -LiteralPath $packageRoot -Force | Where-Object {
        $_.Name -notin @('README.txt', 'Test-Windows.ps1', '.env')
    } | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $runtimeDir -Recurse -Force
    }

    Clear-TestEnvironment
    # Hosted Windows runners may export the variable with an empty value; the
    # backend binds it as a Boolean, so make the first-startup smoke deterministic.
    [Environment]::SetEnvironmentVariable('V2RAYN_WEB_AUTOSTART', 'false', 'Process')
    $url = "http://127.0.0.1:$(Get-FreeLoopbackPort)"
    $webProcess = $null
    $client = $null
    try {
        $exe = Join-Path $runtimeDir $executableName
        $webProcess = Start-NativeTestProcess $exe ('--foreground --no-open --urls "{0}"' -f $url) $runtimeDir
        $health = Wait-TestHealth -Url $url -Process $webProcess

        $lockPath = Join-Path $runtimeDir 'v2rayN.WebAPI.instance.lock'
        $webApiDataDir = Join-Path $runtimeDir 'WebAPIData'
        $tempDataDir = Join-Path $runtimeDir 'guiTemps'
        foreach ($path in @($lockPath, $webApiDataDir, $tempDataDir)) {
            if (-not (Test-Path -LiteralPath $path)) { throw "First-startup data path was not created: $path" }
        }

        Stop-NativeTestProcess $webProcess
        $webProcess.Refresh()
        if (-not $webProcess.HasExited) { throw 'WebAPI process is still running after CTRL_BREAK_EVENT.' }
        if (Get-Process -Id $webProcess.Id -ErrorAction SilentlyContinue) { throw 'WebAPI process remains listed after graceful shutdown.' }

        $lockOwner = (Get-Content -LiteralPath $lockPath -Raw).Trim()
        if ($lockOwner -ne [string]$webProcess.Id) { throw "Instance lock owner mismatch: $lockOwner" }

        $client = New-NoProxyHttpClient
        try {
            $response = $client.GetAsync("$url/api/health").GetAwaiter().GetResult()
            throw "Health endpoint remained reachable after shutdown (HTTP $([int]$response.StatusCode))."
        } catch [System.Net.Http.HttpRequestException] {
            # Expected: Kestrel has closed the listener after graceful shutdown.
        }

        Write-Output "Windows smoke passed: win self-contained EXE, first-run data paths, $url, CTRL+BREAK graceful shutdown."
        Write-Output "Windows: $([Environment]::OSVersion.VersionString); process exited cleanly after CTRL+BREAK."
    } finally {
        if ($null -ne $webProcess) {
            try {
                if (-not $webProcess.HasExited) {
                    try { [void][WebApiWindowsSmokeNative]::GenerateConsoleCtrlEvent(1, [uint32]$webProcess.Id) } catch { }
                    if (-not $webProcess.WaitForExit(5000)) { Stop-Process -Id $webProcess.Id -Force }
                }
            } catch {
                try { Stop-Process -Id $webProcess.Id -Force -ErrorAction SilentlyContinue } catch { }
            }
            $webProcess.Dispose()
        }
        if ($null -ne $client) { $client.Dispose() }
    }
}

# Real helper replacement and health-mismatch rollback with two distinct win-x64 fixtures.
function Invoke-UpdateScenario([bool]$ExpectRollback) {
    $previousRoot = (Resolve-Path -LiteralPath $PreviousPublishDirectory).Path
    $candidateRoot = (Resolve-Path -LiteralPath $CandidatePublishDirectory).Path
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

    $caseRoot = Join-Path $testRoot ([Guid]::NewGuid().ToString('N'))
    $install = Join-Path $caseRoot 'install'
    $candidate = Join-Path $caseRoot ('.v2rayn-WebAPI-candidate-' + [Guid]::NewGuid().ToString('N'))
    $backup = Join-Path $caseRoot ('.v2rayn-WebAPI-backup-' + [Guid]::NewGuid().ToString('N'))
    $core = Join-Path $install 'bin'
    $gui = Join-Path $install 'guiConfigs'
    $temps = Join-Path $install 'guiTemps'
    New-Item -ItemType Directory -Path $install, $candidate, $gui, $temps -Force | Out-Null
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

    $url = "http://127.0.0.1:$(Get-FreeLoopbackPort)"
    $bundleRoot = Join-Path $caseRoot 'bundle'
    New-Item -ItemType Directory -Path $bundleRoot -Force | Out-Null
    $owner = $null
    $helper = $null
    $updatedProcessId = $null
    try {
        Set-UpdateEnvironment $url $key $bundleRoot
        try {
            $exe = Join-Path $install $executableName
            $owner = Start-NativeTestProcess $exe ('--foreground --no-open --urls "{0}"' -f $url) $install
        } finally {
            Restore-TestEnvironment
        }
        $previousHealth = Wait-TestHealth -Url $url -ExpectedVersion $previousAppIdentity.version
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
        Set-UpdateEnvironment $url $key $bundleRoot
        try {
            $startInfo = [Diagnostics.ProcessStartInfo]::new()
            $startInfo.FileName = $helperPath
            $startInfo.WorkingDirectory = $install
            $startInfo.UseShellExecute = $false
            $startInfo.CreateNoWindow = $true
            $startInfo.RedirectStandardOutput = $true
            $startInfo.RedirectStandardError = $true
            $startInfo.Arguments = '"--apply-WebAPI-update" "{0}"' -f $planPath.Replace('"', '\"')
            $helperProcess = [Diagnostics.Process]::Start($startInfo)
            $helperOutput = $helperProcess.StandardOutput.ReadToEndAsync()
            $helperError = $helperProcess.StandardError.ReadToEndAsync()
        } finally {
            Restore-TestEnvironment
        }
        $helper = $helperProcess

        Stop-NativeTestProcess $owner
        $observedHealth = [System.Collections.Generic.List[string]]::new()
        $healthClient = New-NoProxyHttpClient
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
        $helperOutputText = $helperOutput.GetAwaiter().GetResult()
        $helperErrorText = $helperError.GetAwaiter().GetResult()
        if ($helper.ExitCode -ne $expectedExit) {
            $progressDump = if (Test-Path -LiteralPath $progressPath) { Get-Content -LiteralPath $progressPath -Raw } else { '<missing>' }
            throw "Windows update helper exit $($helper.ExitCode), expected $expectedExit.`nhealth observations:`n$($observedHealth -join "`n")`nprogress:`n$progressDump`n$helperOutputText`n$helperErrorText"
        }

        $expectedHealthVersion = if ($ExpectRollback) { $previousAppIdentity.version } else { $candidateAppIdentity.version }
        $health = Wait-TestHealth -Url $url -ExpectedVersion $expectedHealthVersion
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
    } finally {
        foreach ($processId in @($updatedProcessId, $(if ($owner) { $owner.Id } else { $null }))) {
            if ($processId) { Stop-TestProcessAndWait -ProcessId $processId }
        }
        if ($helper) { $helper.Dispose() }
        if ($owner) { $owner.Dispose() }
    }
}

try {
    if ($Mode -eq 'startup') {
        Invoke-StartupSmoke
    } elseif ($Mode -eq 'restore') {
        Invoke-RestoreSmoke
    } else {
        Invoke-UpdateScenario $false | Out-Null
        Invoke-UpdateScenario $true | Out-Null
        Write-Output 'Windows native self-update replacement and rollback smoke passed.'
    }
    $success = $true
} finally {
    Restore-TestEnvironment
    if ($success) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    } else {
        Write-Warning "Windows $Mode test failed; isolated files were retained at $testRoot"
    }
}
