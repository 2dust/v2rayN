param(
    [string]$CurlPath = 'curl.exe'
)
$ErrorActionPreference = 'Stop'
$destination = Join-Path (Split-Path $PSScriptRoot -Parent) 'v2rayN\WinDivert'
$archive = Join-Path $destination 'WinDivert-2.2.2-A.zip'
New-Item -ItemType Directory -Force $destination | Out-Null
$CurlPath = (Get-Command $CurlPath -CommandType Application -ErrorAction Stop).Source
& $CurlPath --fail --location --silent --show-error --connect-timeout 20 --max-time 180 --retry 2 `
    'https://github.com/basil00/WinDivert/releases/download/v2.2.2/WinDivert-2.2.2-A.zip' --output $archive
if ($LASTEXITCODE -ne 0) { throw 'WinDivert download failed.' }
$expected = '63CB41763BB4B20F600B6DE04E991A9C2BE73279E317D4D82F237B150C5F3F15'
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expected) { throw 'WinDivert archive SHA-256 mismatch.' }
Expand-Archive -LiteralPath $archive -DestinationPath $destination -Force
$release = Join-Path $destination 'WinDivert-2.2.2-A'
foreach ($architecture in @('x64', 'x86')) {
    $source = Join-Path $release $architecture
    $target = Join-Path $destination $architecture
    New-Item -ItemType Directory -Force $target | Out-Null
    # The x86 DLL selects the driver for the OS, including x64 Windows under WOW64.
    $drivers = if ($architecture -eq 'x86') { @('WinDivert32.sys', 'WinDivert64.sys') } else { @('WinDivert64.sys') }
    foreach ($name in $drivers) {
        $driver = Join-Path $source $name
        if ((Get-AuthenticodeSignature -LiteralPath $driver).Status -ne 'Valid') { throw "Invalid WinDivert driver signature: $driver" }
        Copy-Item -LiteralPath $driver -Destination $target -Force
    }
    Copy-Item -LiteralPath (Join-Path $source 'WinDivert.dll') -Destination $target -Force
    Copy-Item -LiteralPath (Join-Path $release 'LICENSE') -Destination (Join-Path $target 'WinDivert-LICENSE.txt') -Force
}
Write-Host "Verified WinDivert 2.2.2 installed for publishing at $destination. No driver has been loaded."
