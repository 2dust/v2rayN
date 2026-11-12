[CmdletBinding()]
param(
    [ValidateSet('all', 'x64', 'x86', 'i386', 'arm64')]
    [string]$Arch = 'all',
    [ValidateSet('all', 'wpf', 'desktop')]
    [string]$Flavor = 'all',
    [string]$Version,
    [string]$WixVersion = '7.0.1',
    [string]$Output = 'dist',
    [string]$XrayRef,
    [string]$SingBoxRef
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$script:Stage = 'Initialization'
trap {
    [Console]::Error.WriteLine("v2rayN packaging failed: $script:Stage")
    [Console]::Error.WriteLine($_.Exception.ToString())
    if ($_.ScriptStackTrace) { [Console]::Error.WriteLine($_.ScriptStackTrace) }
    break
}

function Run {
    param([string]$Exe, [string[]]$Arguments)
    & $Exe @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw ('Command failed ({0}): {1} {2}' -f $LASTEXITCODE, $Exe, ($Arguments -join ' '))
    }
}

function Escape-Xml([string]$Value) {
    return [System.Security.SecurityElement]::Escape($Value)
}

function Hash-Id([string]$Value) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($Value.ToLowerInvariant()))
        return ([System.BitConverter]::ToString($bytes, 0, 12)).Replace('-', '')
    } finally { $sha.Dispose() }
}

function Get-Machine([string]$File) {
    if (-not [System.IO.File]::Exists($File)) { throw "Missing executable: $File" }
    $stream = [System.IO.File]::OpenRead($File)
    try {
        $reader = [System.IO.BinaryReader]::new($stream)
        try {
            if ($reader.ReadUInt16() -ne 0x5A4D) { throw "Not a PE file: $File" }
            $stream.Position = 0x3C
            $offset = $reader.ReadInt32()
            if ($offset -lt 64 -or $offset -gt $stream.Length - 6) { throw "Invalid PE offset: $File" }
            $stream.Position = $offset
            if ($reader.ReadUInt32() -ne 0x4550) { throw "Invalid PE signature: $File" }
            return $reader.ReadUInt16()
        } finally { $reader.Dispose() }
    } finally { $stream.Dispose() }
}

function Assert-Machine([string]$File, [int]$Expected) {
    $actual = Get-Machine $File
    if ($actual -ne $Expected) {
        throw ('PE architecture mismatch: {0}, expected 0x{1:X4}, got 0x{2:X4}' -f $File, $Expected, $actual)
    }
}

function Download-File([string]$Url, [string]$Destination) {
    $headers = @{ 'User-Agent' = 'v2rayN-windows-packager'; 'Accept' = 'application/octet-stream' }
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        try {
            Invoke-WebRequest -Uri $Url -Headers $headers -MaximumRedirection 10 -OutFile $Destination -ErrorAction Stop
            if (-not (Test-Path -LiteralPath $Destination -PathType Leaf) -or (Get-Item -LiteralPath $Destination).Length -eq 0) {
                throw "Empty download: $Url"
            }
            return
        } catch {
            if ($attempt -eq 3) { throw }
            Start-Sleep -Seconds (2 * $attempt)
        }
    }
}

function Get-OfficialRelease([string]$Repository, [string]$Ref) {
    if ($Repository -notin @('XTLS/Xray-core', 'SagerNet/sing-box')) {
        throw "Unsupported upstream core repository: $Repository"
    }
    $headers = @{ 'User-Agent' = 'v2rayN-source-packager'; 'Accept' = 'application/vnd.github+json'; 'X-GitHub-Api-Version' = '2022-11-28' }
    if (-not [string]::IsNullOrWhiteSpace($env:GH_TOKEN)) {
        $headers['Authorization'] = "Bearer $env:GH_TOKEN"
    }
    $path = if ([string]::IsNullOrWhiteSpace($Ref)) {
        'releases/latest'
    } else {
        if ($Ref -notmatch '^v?[0-9][A-Za-z0-9._-]{0,100}$') { throw "Invalid core release tag: $Ref" }
        'releases/tags/' + [uri]::EscapeDataString($Ref)
    }
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repository/$path" -Headers $headers
    if (-not $release.tag_name -or -not $release.assets) {
        throw "No usable release assets returned by $Repository ($path)"
    }
    return $release
}

function Download-OfficialCore([object]$Release, [string]$Repository, [string]$AssetName, [string]$ExecutableName, [string]$Destination, [int]$Machine, [string[]]$ExtraFiles, [string]$Scratch) {
    $assetList = @($Release.assets | Where-Object { $_.name -ceq $AssetName })
    if ($assetList.Count -ne 1) {
        throw ("Expected exactly one official {0} asset {1} in release {2}; found {3}. Architecture substitution is forbidden." -f $Repository, $AssetName, $Release.tag_name, $assetList.Count)
    }
    $asset = $assetList[0]
    if (-not $asset.browser_download_url -or $asset.browser_download_url -notlike "https://github.com/$Repository/releases/download/*") {
        throw "Unexpected official download URL: $($asset.browser_download_url)"
    }
    $archive = Join-Path $Scratch $AssetName
    $unpack = Join-Path $Scratch ('unpacked-' + $ExecutableName.Replace('.exe',''))
    New-Item -ItemType Directory -Force -Path $Scratch, $Destination | Out-Null
    if (Test-Path -LiteralPath $unpack) { Remove-Item -LiteralPath $unpack -Recurse -Force }
    Download-File ([string]$asset.browser_download_url) $archive
    if ($asset.digest -and ([string]$asset.digest) -match '^sha256:([A-Fa-f0-9]{64})$') {
        $expected = $Matches[1]
        $actual = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
        if ($actual -ine $expected) { throw "Release asset SHA256 mismatch: $AssetName" }
    }
    Expand-Archive -LiteralPath $archive -DestinationPath $unpack
    $main = @(Get-ChildItem -LiteralPath $unpack -File -Recurse -Filter $ExecutableName)
    if ($main.Count -ne 1) { throw "Expected one $ExecutableName in $AssetName; found $($main.Count)" }
    $destExe = Join-Path $Destination $ExecutableName
    Copy-Item -LiteralPath $main[0].FullName -Destination $destExe -Force
    Assert-Machine $destExe $Machine
    foreach ($extra in $ExtraFiles) {
        $matches = @(Get-ChildItem -LiteralPath $unpack -File -Recurse -Filter $extra)
        if ($matches.Count -gt 1) { throw "Ambiguous optional dependency $extra in $AssetName" }
        if ($matches.Count -eq 1) {
            $dest = Join-Path $Destination $extra
            Copy-Item -LiteralPath $matches[0].FullName -Destination $dest -Force
            if ($extra.EndsWith('.dll', [System.StringComparison]::OrdinalIgnoreCase)) { Assert-Machine $dest $Machine }
        }
    }
    $license = @(Get-ChildItem -LiteralPath $unpack -Recurse -File | Where-Object { $_.Name -match '^(LICENSE|COPYING)(\..+)?$' } | Select-Object -First 1)
    if ($license.Count -eq 1) {
        Copy-Item -LiteralPath $license[0].FullName -Destination (Join-Path $Destination 'LICENSE.txt') -Force
    }
    Write-Host ("Downloaded official {0} {1}: {2}; verified PE 0x{3:X4}" -f $Repository, $Release.tag_name, $AssetName, $Machine)
}

function Download-RuleAssets([string]$Target) {
    $srss = Join-Path $Target 'srss'
    New-Item -ItemType Directory -Force -Path $Target, $srss | Out-Null
    $rules = [ordered]@{
        'geosite.dat' = 'https://github.com/Loyalsoldier/V2ray-rules-dat/releases/latest/download/geosite.dat'
        'geoip.dat' = 'https://github.com/Loyalsoldier/V2ray-rules-dat/releases/latest/download/geoip.dat'
        'geoip-only-cn-private.dat' = 'https://raw.githubusercontent.com/Loyalsoldier/geoip/release/geoip-only-cn-private.dat'
        'Country.mmdb' = 'https://raw.githubusercontent.com/Loyalsoldier/geoip/release/Country.mmdb'
        'geoip.metadb' = 'https://github.com/MetaCubeX/meta-rules-dat/releases/latest/download/geoip.metadb'
    }
    foreach ($fileName in $rules.Keys) { Download-File $rules[$fileName] (Join-Path $Target $fileName) }
    foreach ($f in @('geoip-private.srs','geoip-cn.srs','geoip-facebook.srs','geoip-fastly.srs','geoip-google.srs','geoip-netflix.srs','geoip-telegram.srs','geoip-twitter.srs')) {
        Download-File "https://raw.githubusercontent.com/2dust/sing-box-rules/refs/heads/rule-set-geoip/$f" (Join-Path $srss $f)
    }
    foreach ($f in @('geosite-cn.srs','geosite-gfw.srs','geosite-google.srs','geosite-greatfire.srs','geosite-geolocation-cn.srs','geosite-category-ads-all.srs','geosite-private.srs')) {
        Download-File "https://raw.githubusercontent.com/2dust/sing-box-rules/refs/heads/rule-set-geosite/$f" (Join-Path $srss $f)
    }
}

function New-Logo([string]$Source, [string]$Dest) {
    Add-Type -AssemblyName System.Drawing
    $image = [System.Drawing.Image]::FromFile($Source)
    $bitmap = [System.Drawing.Bitmap]::new(128, 128)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.DrawImage($image, 0, 0, 128, 128)
        $bitmap.Save($Dest, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $graphics.Dispose(); $bitmap.Dispose(); $image.Dispose() }
}

function Build-MsiFileGraph([string]$SourceRoot) {
    $directoryNames = @{}
    foreach ($protected in @('bin', 'guiConfigs', 'guiLogs', 'guiTemps')) { $directoryNames[$protected] = $true }
    $fileRecords = @()
    $sourcePrefix = $SourceRoot.TrimEnd([char]'\', [char]'/') + [System.IO.Path]::DirectorySeparatorChar
    foreach ($file in Get-ChildItem -LiteralPath $SourceRoot -File -Recurse | Sort-Object FullName) {
        $rel = $file.FullName.Substring($sourcePrefix.Length).Replace('/', '\')
        $first = $rel.Split('\')[0]
        if ($first -in @('guiConfigs','guiLogs','guiTemps')) { continue }
        $folder = [System.IO.Path]::GetDirectoryName($rel)
        $fileRecords += [pscustomobject]@{ File = $file; Rel = $rel; Folder = $folder; Protected = ($first -eq 'bin') }
        if ($folder) {
            $segments = $folder.Split('\')
            $cur = ''
            foreach ($segment in $segments) {
                $cur = if ($cur) { "$cur\$segment" } else { $segment }
                $directoryNames[$cur] = $true
            }
        }
    }
    if (@($fileRecords | Where-Object { $_.Rel -ieq 'v2rayN.exe' }).Count -ne 1) {
        throw 'Publish output does not contain v2rayN.exe at the root'
    }
    $dirRefs = [System.Collections.Generic.List[string]]::new()
    foreach ($key in @($directoryNames.Keys | Sort-Object @{ Expression = { $_.Split('\').Count } }, @{ Expression = { $_ } })) {
        $parent = [System.IO.Path]::GetDirectoryName($key)
        $name = [System.IO.Path]::GetFileName($key)
        $id = 'D_' + (Hash-Id $key)
        $parentId = if ($parent) { 'D_' + (Hash-Id $parent) } else { 'INSTALLFOLDER' }
        $dirRefs.Add(('    <DirectoryRef Id="{0}"><Directory Id="{1}" Name="{2}" /></DirectoryRef>' -f $parentId, $id, (Escape-Xml $name)))
    }
    $components = [System.Collections.Generic.List[string]]::new()
    foreach ($item in $fileRecords) {
        $id = 'C_' + (Hash-Id $item.Rel)
        $dirId = if ($item.Folder) { 'D_' + (Hash-Id $item.Folder) } else { 'INSTALLFOLDER' }
        $attributes = if ($item.Protected) { ' Permanent="yes" NeverOverwrite="yes"' } else { '' }
        $components.Add(('      <Component Id="{0}" Directory="{1}" Guid="*"{2}><File Id="F_{3}" Source="{4}" KeyPath="yes" /></Component>' -f $id, $dirId, $attributes, (Hash-Id $item.Rel), (Escape-Xml $item.File.FullName)))
    }
    return @{ Directories = ($dirRefs -join "`n"); Components = ($components -join "`n"); Count = $components.Count }
}

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Join-Path $root 'v2rayN'
$propsFile = Join-Path $projectRoot 'Directory.Build.props'
if (-not (Test-Path $propsFile)) { throw "Cannot find $propsFile. Put this script at the v2rayN repository root." }
if (-not (Test-Path (Join-Path $root 'LICENSE'))) { throw 'Missing GPL-3.0 LICENSE at the repository root.' }
if (-not $Version) {
    $props = Get-Content -LiteralPath $propsFile -Raw
    $m = [regex]::Match($props, '<Version>\s*(\d+\.\d+\.\d+)\s*</Version>')
    if (-not $m.Success) { throw 'Cannot detect v2rayN version from Directory.Build.props. Use -Version.' }
    $Version = $m.Groups[1].Value
}
if ($Version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') { throw 'Version must be X.Y.Z' }
$vp = $Version.Split('.')
if ([int64]$vp[0] -gt 255 -or [int64]$vp[1] -gt 255 -or [int64]$vp[2] -gt 65535) { throw 'MSI version range: 255.255.65535' }
if (-not (Get-Command 'dotnet' -ErrorAction SilentlyContinue)) { throw 'Required build tool not found: dotnet' }
$sdk = @(& dotnet --list-sdks)
if ($LASTEXITCODE -ne 0 -or -not ($sdk -match '^10\.')) { throw '.NET 10 SDK is required.' }
$runtimes = @(& dotnet --list-runtimes)
if ($LASTEXITCODE -ne 0 -or -not ($runtimes -match '^Microsoft\.NETCore\.App 8\.')) { throw 'WiX 7 requires the .NET 8 runtime on the build machine.' }
if ($Arch -eq 'i386') { $Arch = 'x86' }
$arches = if ($Arch -eq 'all') { @('x64','x86','arm64') } else { @($Arch) }
$flavors = if ($Flavor -eq 'all') { @('wpf','desktop') } else { @($Flavor) }
$archMap = @{
    x64 = @{ Rid='win-x64'; Wix='x64'; Machine=0x8664; BurnProgramFiles='ProgramFiles64Folder'; RegBitness='always64' }
    x86 = @{ Rid='win-x86'; Wix='x86'; Machine=0x014c; BurnProgramFiles='ProgramFilesFolder'; RegBitness='always32' }
    arm64 = @{ Rid='win-arm64'; Wix='arm64'; Machine=0xaa64; BurnProgramFiles='ProgramFiles64Folder'; RegBitness='always64' }
}
$ids = @{
    'wpf/x86' = @('197f0b19-86a7-5641-89c8-ba7ac0f26da6','1c7c50eb-7594-59e1-a7c9-24b4e683b14d')
    'wpf/x64' = @('0efa40c2-8f62-51d1-a297-247cc06b62f7','11b3e932-e66a-5c00-93a2-459c2fa363f6')
    'wpf/arm64' = @('ea140a43-0d7f-5c07-abc8-87a17047cdaa','2aeeae41-3746-5c2d-ad00-d6168ae239a8')
    'desktop/x86' = @('f4c7abd0-8d49-5003-8d55-4943327aca61','c14c7a96-7b83-5d54-9c75-7d321fecb74a')
    'desktop/x64' = @('dd0b21a7-0693-5335-b12f-55ef351a0542','0bd4562d-0d96-5c9d-a085-15853e1091f7')
    'desktop/arm64' = @('38a74eff-9918-589e-a4f2-003e27d92daa','5d4e095c-5b69-581d-a503-51f39944648a')
}
foreach ($entry in $ids.GetEnumerator()) {
    if ($entry.Value.Count -ne 2) { throw "Expected MSI and Bundle UpgradeCode GUIDs for $($entry.Key)" }
    foreach ($upgradeCode in $entry.Value) {
        try {
            [void][guid]::ParseExact($upgradeCode, 'D')
        } catch {
            throw "Invalid UpgradeCode GUID for $($entry.Key): $upgradeCode"
        }
    }
}
$work = Join-Path $root '.v2rayn-windows-build'
$script:Work = $work
$dist = if ([System.IO.Path]::IsPathRooted($Output)) { $Output } else { Join-Path $root $Output }
New-Item -ItemType Directory -Force -Path $work, $dist | Out-Null
$wixDir = Join-Path $work 'wix-tools'
$wix = Join-Path $wixDir 'wix.exe'
$script:Stage = 'Installing WiX toolset'
if (Test-Path $wix) {
    $versions = @(& dotnet tool list --tool-path $wixDir)
    $line = @($versions | Where-Object { $_ -match '^\s*wix\s+\S+' } | Select-Object -First 1)
    if ($line.Count -eq 0 -or $line[0] -notmatch ('^\s*wix\s+' + [regex]::Escape($WixVersion) + '\s')) {
        Run 'dotnet' @('tool','update','wix','--tool-path',$wixDir,'--version',$WixVersion)
    }
} else {
    Run 'dotnet' @('tool','install','wix','--tool-path',$wixDir,'--version',$WixVersion)
}
Run $wix @('eula','accept','wix7')
foreach ($ext in @('WixToolset.Util.wixext','WixToolset.BootstrapperApplications.wixext')) {
    Run $wix @('extension','add','-g',"$ext/$WixVersion")
}

$themeTemplate = @'
<?xml version="1.0" encoding="utf-8"?>
<Theme xmlns="http://wixtoolset.org/schemas/v4/thmutil">
  <Font Id="0" Height="-14" Weight="500" Foreground="292222" Background="ffffff">Segoe UI</Font>
  <Font Id="1" Height="-28" Weight="650" Foreground="252020" Background="ffffff">Segoe UI</Font>
  <Font Id="2" Height="-14" Weight="450" Foreground="777070" Background="ffffff">Segoe UI</Font>
  <Font Id="3" Height="-15" Weight="600" Foreground="966863" Background="ffffff">Segoe UI</Font>
  <Font Id="4" Height="-16" Weight="450" Foreground="363030" Background="ffffff">Segoe UI</Font>
  <Window Width="640" Height="430" HexStyle="100a0000" FontId="0" Caption="#(loc.Caption)">
    <ImageControl X="32" Y="27" Width="64" Height="64" ImageFile="logo.png" Visible="yes" />
    <Label X="112" Y="29" Width="-32" Height="44" FontId="1" DisablePrefix="yes">v2rayN</Label>
    <Label X="114" Y="77" Width="-32" Height="29" FontId="3" DisablePrefix="yes">#(loc.Tagline)</Label>
    <Page Name="Loading">
      <Label X="32" Y="146" Width="-32" Height="42" FontId="4">#(loc.Preparing)</Label>
      <Label Name="CheckingForUpdatesLabel" X="32" Y="195" Width="-32" Height="30" FontId="2" Visible="no" />
    </Page>
    <Page Name="Help">
      <Label X="32" Y="141" Width="-32" Height="42" FontId="1">#(loc.HelpHeader)</Label>
      <Label X="32" Y="203" Width="-32" Height="64" FontId="4">#(loc.HelpMessage)</Label>
      <Button Name="HelpCloseButton" X="-30" Y="-28" Width="110" Height="36" TabStop="yes" FontId="0">
        <Text>#(loc.Close)</Text><CloseWindowAction />
      </Button>
    </Page>
    <Page Name="Install">
      <Label X="32" Y="141" Width="-32" Height="46" FontId="1">#(loc.InstallHeader)</Label>
      <Label X="32" Y="201" Width="-32" Height="65" FontId="4">#(loc.InstallMessage)</Label>
      <Label Name="InstallVersion" X="32" Y="274" Width="-32" Height="28" FontId="2" VisibleCondition="WixStdBAShowVersion">#(loc.VersionText)</Label>
      <Label X="32" Y="307" Width="-32" Height="44" FontId="2" VisibleCondition="PreviousInstallFolder">#(loc.UpgradeFolder)</Label>
      <Button Name="OptionsButton" X="32" Y="-28" Width="124" Height="36" TabStop="yes" FontId="0" VisibleCondition="NOT PreviousInstallFolder">
        <Text>#(loc.Options)</Text><ChangePageAction Page="Options" />
      </Button>
      <Button Name="InstallButton" X="-158" Y="-28" Width="120" Height="36" TabStop="yes" FontId="0" ElevationShieldCondition='(WixBundleAuthoredScope = 1 AND NOT WixBundleElevated) OR ((WixBundleAuthoredScope = 2 OR WixBundleAuthoredScope = 3) AND WixStdBAScope = "PerMachine")'>#(loc.Install)</Button>
      <Button Name="InstallCancelButton" X="-30" Y="-28" Width="110" Height="36" TabStop="yes" FontId="0">
        <Text>#(loc.Close)</Text><CloseWindowAction />
      </Button>
    </Page>
    <Page Name="Options">
      <Label X="32" Y="130" Width="-32" Height="48" FontId="1">#(loc.OptionsHeader)</Label>
      <Label X="32" Y="183" Width="-32" Height="27" FontId="2">#(loc.InstallLocation)</Label>
      <Editbox Name="InstallFolder" X="32" Y="215" Width="432" Height="36" TabStop="yes" FontId="0" FileSystemAutoComplete="yes" />
      <Button Name="BrowseButton" X="478" Y="215" Width="130" Height="36" TabStop="yes" FontId="0">
        <Text>#(loc.Browse)</Text><BrowseDirectoryAction VariableName="InstallFolder" />
      </Button>
      <Checkbox Name="CreateDesktopShortcut" X="32" Y="273" Width="-32" Height="32" TabStop="yes" FontId="4">#(loc.DesktopShortcut)</Checkbox>
      <Checkbox Name="CreateStartMenuShortcut" X="32" Y="312" Width="-32" Height="32" TabStop="yes" FontId="4">#(loc.StartShortcut)</Checkbox>
      <Button Name="OptionsOkButton" X="-158" Y="-28" Width="120" Height="36" TabStop="yes" FontId="0">
        <Text>#(loc.Save)</Text><ChangePageAction Page="Install" />
      </Button>
      <Button Name="OptionsCancelButton" X="-30" Y="-28" Width="110" Height="36" TabStop="yes" FontId="0">
        <Text>#(loc.Back)</Text><ChangePageAction Page="Install" Cancel="yes" />
      </Button>
    </Page>
    <Page Name="Modify">
      <Label X="32" Y="143" Width="-32" Height="48" FontId="1">#(loc.ModifyHeader)</Label>
      <Label X="32" Y="208" Width="-32" Height="72" FontId="4">#(loc.ModifyMessage)</Label>
      <Button Name="RepairButton" X="32" Y="-28" Width="124" Height="36" TabStop="yes" FontId="0" HideWhenDisabled="yes">#(loc.Repair)</Button>
      <Button Name="UninstallButton" X="174" Y="-28" Width="124" Height="36" TabStop="yes" FontId="0">#(loc.Uninstall)</Button>
      <Button Name="ModifyCancelButton" X="-30" Y="-28" Width="110" Height="36" TabStop="yes" FontId="0">
        <Text>#(loc.Close)</Text><CloseWindowAction />
      </Button>
    </Page>
    <Page Name="Progress">
      <Label X="32" Y="142" Width="-32" Height="47" FontId="1">#(loc.ProgressHeader)</Label>
      <Label Name="OverallProgressPackageText"
             X="32" Y="209"
             Width="-32" Height="38"
             FontId="2"
             DisablePrefix="yes" />
      <Progressbar Name="OverallCalculatedProgressbar" X="32" Y="272" Width="-32" Height="17" />
      <Button Name="ProgressCancelButton" X="-30" Y="-28" Width="110" Height="36" TabStop="yes" FontId="0">#(loc.Cancel)</Button>
    </Page>
    <Page Name="Success">
      <Label X="32" Y="143" Width="-32" Height="49" FontId="1">
        <Text>#(loc.SuccessHeader)</Text>
        <Text Condition="WixBundleAction = 3">#(loc.SuccessUninstall)</Text>
        <Text Condition="WixBundleAction = 5">#(loc.SuccessInstall)</Text>
        <Text Condition="WixBundleAction = 7">#(loc.SuccessRepair)</Text>
      </Label>
      <Label X="32" Y="209" Width="-32" Height="65" FontId="4">#(loc.SuccessMessage)</Label>
      <Button Name="LaunchButton" X="32" Y="-28" Width="164" Height="36" TabStop="yes" FontId="0" HideWhenDisabled="yes" VisibleCondition="WixBundleAction = 5">#(loc.Launch)</Button>
      <Label X="32" Y="309" Width="-32" Height="32" FontId="2" VisibleCondition="WixStdBARestartRequired">#(loc.RestartNeeded)</Label>
      <Button Name="SuccessRestartButton" X="-158" Y="-28" Width="120" Height="36" TabStop="yes" FontId="0" HideWhenDisabled="yes">#(loc.Restart)</Button>
      <Button Name="SuccessCloseButton" X="-30" Y="-28" Width="110" Height="36" TabStop="yes" FontId="0">
        <Text>#(loc.Done)</Text><CloseWindowAction />
      </Button>
    </Page>
    <Page Name="Failure">
      <Label X="32" Y="143" Width="-32" Height="50" FontId="1">#(loc.FailureHeader)</Label>
      <Label X="32" Y="205" Width="-32" Height="40" FontId="4">#(loc.FailureMessage)</Label>
      <Hypertext Name="FailureLogFileLink" X="32" Y="253" Width="-32" Height="31" FontId="3" TabStop="yes" HideWhenDisabled="yes">#(loc.FailureHyperlinkLogText)</Hypertext>
      <Hypertext Name="FailureMessageText" X="32" Y="292" Width="-32" Height="58" FontId="2" TabStop="yes" HideWhenDisabled="yes" />
      <Button Name="FailureRestartButton" X="-158" Y="-28" Width="120" Height="36" TabStop="yes" FontId="0" HideWhenDisabled="yes">#(loc.Restart)</Button>
      <Button Name="FailureCloseButton" X="-30" Y="-28" Width="110" Height="36" TabStop="yes" FontId="0">
        <Text>#(loc.Close)</Text><CloseWindowAction />
      </Button>
    </Page>
  </Window>
</Theme>
'@

[void][System.Xml.Linq.XDocument]::Parse($themeTemplate)
$script:Stage = 'Validating four-language translations'

$themeCultures = @(
    @{ Culture = 'en-US'; Lcid = 1033 },
    @{ Culture = 'zh-CN'; Lcid = 2052 },
    @{ Culture = 'zh-TW'; Lcid = 1028 },
    @{ Culture = 'fr-FR'; Lcid = 1036 }
)
$themeTranslations = [ordered]@{
    Caption = @('v2rayN Setup', 'v2rayN 安装程序', 'v2rayN 安裝程式', 'Installation de v2rayN')
    Tagline = @('A modern proxy client', '现代代理客户端', '現代代理用戶端', 'Un client proxy moderne')
    Preparing = @('Preparing setup…', '正在准备安装…', '正在準備安裝…', 'Préparation de l''installation…')
    HelpHeader = @('v2rayN setup', 'v2rayN 安装程序', 'v2rayN 安裝程式', 'Installation de v2rayN')
    HelpMessage = @('Install, repair, or remove v2rayN.', '安装、修复或卸载 v2rayN。', '安裝、修復或解除安裝 v2rayN。', 'Installez, réparez ou désinstallez v2rayN.')
    Close = @('Close', '关闭', '關閉', 'Fermer')
    InstallHeader = @('Ready to install', '准备安装', '準備安裝', 'Prêt à installer')
    InstallMessage = @('Proxy management for Windows.', 'Windows 代理管理工具。', 'Windows 代理管理工具。', 'Gestion de proxy pour Windows.')
    VersionText = @('Version [WixBundleVersion]', '版本 [WixBundleVersion]', '版本 [WixBundleVersion]', 'Version [WixBundleVersion]')
    UpgradeFolder = @('The existing installation folder will be kept during the update.', '更新时将保留现有安装目录。', '更新時將保留現有安裝目錄。', 'Le dossier d''installation actuel sera conservé pendant la mise à jour.')
    Options = @('Options', '选项', '選項', 'Options')
    Install = @('Install', '安装', '安裝', 'Installer')
    OptionsHeader = @('Installation options', '安装选项', '安裝選項', 'Options d''installation')
    InstallLocation = @('Install location', '安装位置', '安裝位置', 'Dossier d''installation')
    Browse = @('Browse…', '浏览…', '瀏覽…', 'Parcourir…')
    DesktopShortcut = @('Create a desktop shortcut', '创建桌面快捷方式', '建立桌面捷徑', 'Créer un raccourci sur le Bureau')
    StartShortcut = @('Create a Start menu shortcut', '创建开始菜单快捷方式', '建立開始功能表捷徑', 'Créer un raccourci dans le menu Démarrer')
    Save = @('Save', '保存', '儲存', 'Enregistrer')
    Back = @('Back', '返回', '返回', 'Retour')
    ModifyHeader = @('v2rayN is installed', 'v2rayN 已安装', 'v2rayN 已安裝', 'v2rayN est installé')
    ModifyMessage = @('You can repair the installation or uninstall v2rayN.', '可以修复安装或卸载 v2rayN。', '您可以修復安裝或解除安裝 v2rayN。', 'Vous pouvez réparer ou désinstaller v2rayN.')
    Repair = @('Repair', '修复', '修復', 'Réparer')
    Uninstall = @('Uninstall', '卸载', '解除安裝', 'Désinstaller')
    ProgressHeader = @('Setting things up', '正在配置', '正在設定', 'Configuration en cours')
    ProgressPackage = @('Installing v2rayN…', '正在安装 v2rayN…', '正在安裝 v2rayN…', 'Installation de v2rayN…')
    Cancel = @('Cancel', '取消', '取消', 'Annuler')
    SuccessHeader = @('Operation completed', '操作已完成', '作業已完成', 'Opération terminée')
    SuccessUninstall = @('v2rayN was uninstalled', 'v2rayN 已卸载', 'v2rayN 已解除安裝', 'v2rayN a été désinstallé')
    SuccessInstall = @('v2rayN is ready', 'v2rayN 已准备就绪', 'v2rayN 已準備就緒', 'v2rayN est prêt')
    SuccessRepair = @('v2rayN was repaired', 'v2rayN 已修复', 'v2rayN 已修復', 'v2rayN a été réparé')
    SuccessMessage = @('Everything finished successfully.', '操作已成功完成。', '作業已成功完成。', 'L''opération s''est terminée avec succès.')
    Launch = @('Launch v2rayN', '启动 v2rayN', '啟動 v2rayN', 'Lancer v2rayN')
    RestartNeeded = @('Restart Windows to finish setup.', '请重启 Windows 以完成安装。', '請重新啟動 Windows 以完成安裝。', 'Redémarrez Windows pour terminer l''installation.')
    Restart = @('Restart', '重启', '重新啟動', 'Redémarrer')
    Done = @('Done', '完成', '完成', 'Terminer')
    FailureHeader = @('Setup could not finish', '安装未能完成', '安裝未能完成', 'Impossible de terminer l''installation')
    FailureMessage = @('Windows Installer reported an error.', 'Windows Installer 报告了错误。', 'Windows Installer 回報了錯誤。', 'Windows Installer a signalé une erreur.')
    ViewLog = @('View installation log', '查看安装日志', '檢視安裝記錄', 'Afficher le journal d''installation')
    ConfirmCancelMessage = @('Are you sure you want to cancel?', '确定要取消吗？', '確定要取消嗎？', 'Voulez-vous vraiment annuler ?')
    ExecuteUpgradeRelatedBundleMessage = @('Previous version', '以前的版本', '舊版', 'Version précédente')
    FilesInUseTitle = @('Files in use', '文件正在使用中', '檔案正在使用中', 'Fichiers en cours d''utilisation')
    FilesInUseLabel = @('These apps are using files that must be updated:', '以下应用程序正在使用需要更新的文件：', '下列應用程式正在使用需要更新的檔案：', 'Les applications suivantes utilisent des fichiers à mettre à jour :')
    FilesInUseNetfxCloseRadioButton = @('Close the applications.', '关闭这些应用程序。', '關閉這些應用程式。', 'Fermer les applications.')
    FilesInUseCloseRadioButton = @('Close the applications and try to restart them.', '关闭这些应用程序并尝试重新启动。', '關閉這些應用程式並嘗試重新啟動。', 'Fermer les applications et essayer de les redémarrer.')
    FilesInUseDontCloseRadioButton = @('Do not close applications; a restart will be required.', '不关闭应用程序；稍后需要重启。', '不要關閉應用程式；稍後需要重新啟動。', 'Ne pas fermer les applications ; un redémarrage sera nécessaire.')
    FilesInUseRetryButton = @('Retry', '重试', '重試', 'Réessayer')
    FilesInUseIgnoreButton = @('Ignore', '忽略', '忽略', 'Ignorer')
    FilesInUseExitButton = @('Exit', '退出', '結束', 'Quitter')
    FailureHyperlinkLogText = @('View the <a href="#">installation log</a>', '查看<a href="#">安装日志</a>', '檢視<a href="#">安裝記錄</a>', 'Afficher le <a href="#">journal d''installation</a>')
}
$themeTokens = [regex]::Matches($themeTemplate, '#\(loc\.([A-Za-z][A-Za-z0-9_]*)\)') | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique
foreach ($token in $themeTokens) {
    if (-not $themeTranslations.Contains($token)) { throw "Missing localized string: $token" }
}
$localFiles = @{}
$localizationEntries = @($themeTranslations.GetEnumerator())
foreach ($entry in $localizationEntries) {
    $values = @($entry.Value)
    if ($values.Count -ne $themeCultures.Count) {
        throw ("Translation '{0}' has {1} values; expected {2}" -f $entry.Key, $values.Count, $themeCultures.Count)
    }
}
for ($index = 0; $index -lt $themeCultures.Count; $index++) {
    $culture = $themeCultures[$index]
    $script:Stage = "Generating localization for $($culture.Culture) (index $index)"
    $path = Join-Path $work ("$($culture.Culture).thm.wxl")
    $strings = foreach ($entry in $localizationEntries) {
        $values = @($entry.Value)
        $value = Escape-Xml ([string]$values[$index])
        "  <String Id=`"$($entry.Key)`" Value=`"$value`" />"
    }
    $xml = "<?xml version=`"1.0`" encoding=`"utf-8`"?>`n<WixLocalization xmlns=`"http://wixtoolset.org/schemas/v4/wxl`" Culture=`"$($culture.Culture)`" Language=`"$($culture.Lcid)`" Codepage=`"65001`">`n$($strings -join "`n")`n</WixLocalization>"
    [void][System.Xml.Linq.XDocument]::Parse($xml)
    [System.IO.File]::WriteAllText($path, $xml, [System.Text.UTF8Encoding]::new($false))
    $localFiles[$culture.Culture] = $path
}
$script:Stage = 'Mapping localization payloads'
$localizedPayloads = @()
$localeMappings = [ordered]@{
    '1033' = 'en-US'
    '2052' = 'zh-CN'
    '4100' = 'zh-CN'
    '1028' = 'zh-TW'
    '3076' = 'zh-TW'
    '5124' = 'zh-TW'
    '1036' = 'fr-FR'
    '2060' = 'fr-FR'
    '3084' = 'fr-FR'
    '4108' = 'fr-FR'
    '5132' = 'fr-FR'
}
foreach ($mapping in $localeMappings.GetEnumerator()) {
    $lcid = [string]$mapping.Key
    $cultureName = [string]$mapping.Value
    if (-not $localFiles.ContainsKey($cultureName)) {
        throw "Missing localization file for $cultureName (LCID $lcid)"
    }
    $source = Escape-Xml $localFiles[$cultureName]
    $localizedPayloads += "      <Payload Id=`"v2rayNLocale$lcid`" Name=`"${lcid}\thm.wxl`" SourceFile=`"$source`" Compressed=`"yes`" />"
}
$localePayloadXml = $localizedPayloads -join "`n"



$script:Stage = 'Resolving official core releases'
$xrayRelease = Get-OfficialRelease 'XTLS/Xray-core' $XrayRef
$singRelease = Get-OfficialRelease 'SagerNet/sing-box' $SingBoxRef
$singVersion = ([string]$singRelease.tag_name) -replace '^v', ''
Write-Host "Official core releases: Xray $($xrayRelease.tag_name); sing-box $($singRelease.tag_name)"

$themePath = Join-Path $work 'v2rayN-theme.xml'
[System.IO.File]::WriteAllText($themePath, $themeTemplate, [System.Text.UTF8Encoding]::new($false))


$originalLocation = Get-Location
try {
    Set-Location $root
    foreach ($arch in $arches) {
        $map = $archMap[$arch]
        $binSource = Join-Path $work "core-$arch"
        $script:Stage = "Downloading $arch native Xray-core and sing-box from upstream GitHub Releases"
        if (Test-Path -LiteralPath $binSource) { Remove-Item -LiteralPath $binSource -Recurse -Force }
        $xrayDest = Join-Path $binSource 'xray'
        $singDest = Join-Path $binSource 'sing_box'
        New-Item -ItemType Directory -Force -Path $xrayDest, $singDest | Out-Null
        $coreScratch = Join-Path $work "downloads-$arch"
        if (Test-Path -LiteralPath $coreScratch) { Remove-Item -LiteralPath $coreScratch -Recurse -Force }
        $xrayAsset = @{ x86='Xray-windows-32.zip'; x64='Xray-windows-64.zip'; arm64='Xray-windows-arm64-v8a.zip' }[$arch]
        $singSuffix = @{ x86='386'; x64='amd64'; arm64='arm64' }[$arch]
        $singAsset = "sing-box-$singVersion-windows-$singSuffix.zip"
        Download-OfficialCore $xrayRelease 'XTLS/Xray-core' $xrayAsset 'xray.exe' $xrayDest $map.Machine @('wintun.dll') (Join-Path $coreScratch 'xray')
        Download-OfficialCore $singRelease 'SagerNet/sing-box' $singAsset 'sing-box.exe' $singDest $map.Machine @('libcronet.dll') (Join-Path $coreScratch 'sing-box')
        $script:Stage = "Downloading $arch rule databases (non-executable assets)"
        Download-RuleAssets $binSource
        foreach ($flavor in $flavors) {
            $script:Stage = "Publishing $flavor $arch"
            $project = if ($flavor -eq 'wpf') { 'v2rayN/v2rayN/v2rayN.csproj' } else { 'v2rayN/v2rayN.Desktop/v2rayN.Desktop.csproj' }
            $iconSource = if ($flavor -eq 'wpf') { 'v2rayN/v2rayN/Resources/v2rayN.ico' } else { 'v2rayN/v2rayN.Desktop/Assets/v2rayN.ico' }
            $out = Join-Path $work "publish-$flavor-$arch"
            if (Test-Path $out) { Remove-Item -LiteralPath $out -Recurse -Force }
            New-Item -ItemType Directory -Force -Path $out | Out-Null
            Run 'dotnet' @('restore', $project, '-r', $map.Rid)
            Run 'dotnet' @(
                'publish', $project,
                '-c', 'Release',
                '-r', $map.Rid,
                '-p:SelfContained=true',
                '-p:EnableWindowsTargeting=true',
                '-o', $out
            )
            Run 'dotnet' @(
                'publish', './v2rayN/AmazTool/AmazTool.csproj',
                '-c', 'Release',
                '-r', $map.Rid,
                '-p:SelfContained=true',
                '-p:PublishTrimmed=true',
                '-p:EnableWindowsTargeting=true',
                '-o', $out
            )
            Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $out 'LICENSE.txt') -Force
            Assert-Machine (Join-Path $out 'v2rayN.exe') $map.Machine
            Assert-Machine (Join-Path $out 'AmazTool.exe') $map.Machine
            $binDest = Join-Path $out 'bin'
            New-Item -ItemType Directory -Force -Path $binDest | Out-Null
            Copy-Item -Path (Join-Path $binSource '*') -Destination $binDest -Recurse -Force
            foreach ($core in @('xray/xray.exe', 'sing_box/sing-box.exe')) {
                $corePath = Join-Path $binDest $core
                if (-not (Test-Path -LiteralPath $corePath)) { throw "Required native core missing: $corePath" }
                Assert-Machine $corePath $map.Machine
            }
            $graph = Build-MsiFileGraph $out
            Write-Host "MSI files: $($graph.Count) (bin is permanent and never overwritten)"
            $flavorLabel = if ($flavor -eq 'wpf') { 'WPF' } else { 'Desktop' }
            $folderName = if ($flavor -eq 'wpf') { 'v2rayN' } else { 'v2rayN Desktop' }
            $regKey = "Software\v2rayN\Installer\$flavor\$arch"
            $msiGuid = $ids["$flavor/$arch"][0]
            $bundleGuid = $ids["$flavor/$arch"][1]
            $iconPath = Join-Path $root $iconSource
            if (-not (Test-Path $iconPath)) { throw "Missing icon: $iconPath" }
            $logo = Join-Path $work "$flavor-$arch-logo.png"
            Add-Type -AssemblyName System.Drawing
            $iconObject = [System.Drawing.Icon]::new($iconPath)
            $iconBitmap = $iconObject.ToBitmap()
            try { $iconBitmap.Save($logo, [System.Drawing.Imaging.ImageFormat]::Png) }
            finally { $iconBitmap.Dispose(); $iconObject.Dispose() }
            $shortcutName = if ($flavor -eq 'wpf') { 'v2rayN' } else { 'v2rayN Desktop' }
            $templateVars = @{
                'NAME' = Escape-Xml "v2rayN $flavorLabel ($arch)"
                'SHORTCUT_NAME' = Escape-Xml $shortcutName
                'VERSION' = Escape-Xml $Version
                'MSIGUID' = Escape-Xml $msiGuid
                'BUNDLEGUID' = Escape-Xml $bundleGuid
                'DIRNAME' = Escape-Xml $folderName
                'BURN_PROGRAMFILES' = $map.BurnProgramFiles
                'REG_BITNESS' = $map.RegBitness
                'REGKEY' = Escape-Xml $regKey
                'ICON' = Escape-Xml $iconPath
                'MSI' = Escape-Xml (Join-Path $work "$flavor-$arch.msi")
                'LOGO' = Escape-Xml $logo
                'MACHINE' = Escape-Xml $map.Machine
                'DIRS' = $graph.Directories
                'FILES' = $graph.Components
                'THEME' = Escape-Xml $themePath
                'LOCALIZATION' = Escape-Xml $localFiles['en-US']
                'LOCALPAYLOADS' = $localePayloadXml
            }
            $msiText = @'
<?xml version="1.0" encoding="utf-8"?>
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs" xmlns:util="http://wixtoolset.org/schemas/v4/wxs/util">
  <Package Name="{{NAME}}" Manufacturer="2dust" Version="{{VERSION}}" UpgradeCode="{{MSIGUID}}" Scope="perMachine" InstallerVersion="500" Language="1033">
    <MediaTemplate EmbedCab="yes" />
    <MajorUpgrade Schedule="afterInstallInitialize" DowngradeErrorMessage="A newer version of v2rayN is installed." />
    <util:CloseApplication Id="CloseV2rayN" Target="v2rayN.exe" CloseMessage="yes" PromptToContinue="yes" Description="Save your changes and close v2rayN before continuing." Timeout="20" RebootPrompt="yes" />
    <Property Id="CREATE_DESKTOP_SHORTCUT" Value="1" Secure="yes" />
    <Property Id="CREATE_STARTMENU_SHORTCUT" Value="1" Secure="yes" />
    <Property Id="PREVIOUS_DIR" Secure="yes"><RegistrySearch Id="FindPreviousDir" Root="HKLM" Key="{{REGKEY}}" Name="InstallDir" Type="raw" /></Property>
    <Property Id="CLEAN_BIN"><RegistrySearch Id="FindBin" Root="HKLM" Key="{{REGKEY}}" Name="BinDir" Type="raw" /></Property>
    <Property Id="CLEAN_CONFIG"><RegistrySearch Id="FindConfig" Root="HKLM" Key="{{REGKEY}}" Name="ConfigDir" Type="raw" /></Property>
    <Property Id="CLEAN_LOGS"><RegistrySearch Id="FindLogs" Root="HKLM" Key="{{REGKEY}}" Name="LogsDir" Type="raw" /></Property>
    <Property Id="CLEAN_TEMPS"><RegistrySearch Id="FindTemps" Root="HKLM" Key="{{REGKEY}}" Name="TempsDir" Type="raw" /></Property>
    <Launch Condition="NOT WIX_UPGRADE_DETECTED OR PREVIOUS_DIR" Message="Cannot read the previous installation directory." />
    <SetDirectory Id="INSTALLFOLDER" Value="[PREVIOUS_DIR]" Condition="WIX_UPGRADE_DETECTED AND PREVIOUS_DIR" />
    <StandardDirectory Id="ProgramFiles6432Folder">
      <Directory Id="INSTALLFOLDER" Name="{{DIRNAME}}" />
    </StandardDirectory>
    <StandardDirectory Id="ProgramMenuFolder"><Directory Id="V2RAYNMENU" Name="{{SHORTCUT_NAME}}" /></StandardDirectory>
    <Component Id="DirState" Directory="INSTALLFOLDER" Guid="*">
      <CreateFolder Directory="D_{{BIN_ID}}" />
      <CreateFolder Directory="D_{{CONFIG_ID}}" />
      <CreateFolder Directory="D_{{LOGS_ID}}" />
      <CreateFolder Directory="D_{{TEMPS_ID}}" />
      <RegistryValue Root="HKLM" Key="{{REGKEY}}" Name="InstallDir" Value="[INSTALLFOLDER]" Type="string" KeyPath="yes" />
      <RegistryValue Root="HKLM" Key="{{REGKEY}}" Name="BinDir" Value="[INSTALLFOLDER]bin" Type="string" />
      <RegistryValue Root="HKLM" Key="{{REGKEY}}" Name="ConfigDir" Value="[INSTALLFOLDER]guiConfigs" Type="string" />
      <RegistryValue Root="HKLM" Key="{{REGKEY}}" Name="LogsDir" Value="[INSTALLFOLDER]guiLogs" Type="string" />
      <RegistryValue Root="HKLM" Key="{{REGKEY}}" Name="TempsDir" Value="[INSTALLFOLDER]guiTemps" Type="string" />
      <RegistryValue Root="HKLM" Key="{{REGKEY}}" Name="DesktopEnabled" Value="[CREATE_DESKTOP_SHORTCUT]" Type="string" />
      <RegistryValue Root="HKLM" Key="{{REGKEY}}" Name="StartMenuEnabled" Value="[CREATE_STARTMENU_SHORTCUT]" Type="string" />
      <util:RemoveFolderEx Id="CleanBin" Property="CLEAN_BIN" On="uninstall" Condition="REMOVE=&quot;ALL&quot; AND NOT UPGRADINGPRODUCTCODE" />
      <util:RemoveFolderEx Id="CleanConfigs" Property="CLEAN_CONFIG" On="uninstall" Condition="REMOVE=&quot;ALL&quot; AND NOT UPGRADINGPRODUCTCODE" />
      <util:RemoveFolderEx Id="CleanLogs" Property="CLEAN_LOGS" On="uninstall" Condition="REMOVE=&quot;ALL&quot; AND NOT UPGRADINGPRODUCTCODE" />
      <util:RemoveFolderEx Id="CleanTemps" Property="CLEAN_TEMPS" On="uninstall" Condition="REMOVE=&quot;ALL&quot; AND NOT UPGRADINGPRODUCTCODE" />
      <RemoveFolder Id="RemoveAppFolder" On="uninstall" />
    </Component>
    <Component Id="DesktopLink" Directory="DesktopFolder" Guid="*">
      <Shortcut Id="DesktopShortcut" Name="{{SHORTCUT_NAME}}" Target="[INSTALLFOLDER]v2rayN.exe" WorkingDirectory="INSTALLFOLDER" />
      <RegistryValue Root="HKLM" Key="{{REGKEY}}" Name="DesktopLink" Value="1" Type="integer" KeyPath="yes" />
    </Component>
    <Component Id="StartMenuLink" Directory="V2RAYNMENU" Guid="*">
      <Shortcut Id="StartMenuShortcut" Name="{{SHORTCUT_NAME}}" Target="[INSTALLFOLDER]v2rayN.exe" WorkingDirectory="INSTALLFOLDER" />
      <RemoveFolder Id="RemoveMenu" On="uninstall" />
      <RegistryValue Root="HKLM" Key="{{REGKEY}}" Name="StartMenuLink" Value="1" Type="integer" KeyPath="yes" />
    </Component>
    <Feature Id="Main" Title="v2rayN" Level="1" AllowAbsent="no" Display="expand" AllowAdvertise="no" ConfigurableDirectory="INSTALLFOLDER">
      <ComponentGroupRef Id="AllFiles" />
      <ComponentRef Id="DirState" />
      <Feature Id="DesktopOpt" Title="Desktop shortcut" Level="1" AllowAdvertise="no">
        <Level Condition="CREATE_DESKTOP_SHORTCUT = &quot;0&quot;" Value="0" />
        <ComponentRef Id="DesktopLink" />
      </Feature>
      <Feature Id="StartOpt" Title="Start menu shortcut" Level="1" AllowAdvertise="no">
        <Level Condition="CREATE_STARTMENU_SHORTCUT = &quot;0&quot;" Value="0" />
        <ComponentRef Id="StartMenuLink" />
      </Feature>
    </Feature>
    <Icon Id="AppIcon" SourceFile="{{ICON}}" />
    <Property Id="ARPPRODUCTICON" Value="AppIcon" />
  </Package>
  <Fragment>
{{DIRS}}
    <ComponentGroup Id="AllFiles">
{{FILES}}
    </ComponentGroup>
  </Fragment>
</Wix>
'@
            $templateVars['BIN_ID'] = Hash-Id 'bin'
            $templateVars['CONFIG_ID'] = Hash-Id 'guiConfigs'
            $templateVars['LOGS_ID'] = Hash-Id 'guiLogs'
            $templateVars['TEMPS_ID'] = Hash-Id 'guiTemps'
            foreach ($key in $templateVars.Keys) { $msiText = $msiText.Replace('{{' + $key + '}}', [string]$templateVars[$key]) }
            [void][System.Xml.Linq.XDocument]::Parse($msiText)
            $msiSource = Join-Path $work "$flavor-$arch.wxs"
            [System.IO.File]::WriteAllText($msiSource, $msiText, [System.Text.UTF8Encoding]::new($false))
            $script:Stage = "Building $flavor $arch MSI"
            Run $wix @('build',$msiSource,'-arch',$map.Wix,'-ext','WixToolset.Util.wixext','-o',$templateVars.MSI)
            $bundleText = @'
<?xml version="1.0" encoding="utf-8"?>
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs" xmlns:bal="http://wixtoolset.org/schemas/v4/wxs/bal" xmlns:util="http://wixtoolset.org/schemas/v4/wxs/util">
  <Bundle Name="{{NAME}}" Manufacturer="2dust" Version="{{VERSION}}" UpgradeCode="{{BUNDLEGUID}}" IconSourceFile="{{ICON}}">
    <Variable Name="InstallFolder" Type="formatted" Value="[{{BURN_PROGRAMFILES}}]{{DIRNAME}}" />
    <Variable Name="CreateDesktopShortcut" Type="numeric" Value="1" />
    <Variable Name="CreateStartMenuShortcut" Type="numeric" Value="1" />
    <util:RegistrySearch Id="GetInstallFolder" Root="HKLM" Bitness="{{REG_BITNESS}}" Key="{{REGKEY}}" Value="InstallDir" Variable="PreviousInstallFolder" Result="value" />
    <util:RegistrySearch Id="GetDesktop" Root="HKLM" Bitness="{{REG_BITNESS}}" Key="{{REGKEY}}" Value="DesktopEnabled" Variable="PreviousDesktopEnabled" Result="value" />
    <util:RegistrySearch Id="GetStart" Root="HKLM" Bitness="{{REG_BITNESS}}" Key="{{REGKEY}}" Value="StartMenuEnabled" Variable="PreviousStartMenuEnabled" Result="value" />
    <SetVariable Id="RestoreFolder" Variable="InstallFolder" Type="formatted" Value="[PreviousInstallFolder]" Condition="PreviousInstallFolder" After="GetInstallFolder" />
    <SetVariable Id="RestoreDesktop" Variable="CreateDesktopShortcut" Type="numeric" Value="[PreviousDesktopEnabled]" Condition='PreviousDesktopEnabled = "0" OR PreviousDesktopEnabled = "1"' After="GetDesktop" />
    <SetVariable Id="RestoreStart" Variable="CreateStartMenuShortcut" Type="numeric" Value="[PreviousStartMenuEnabled]" Condition='PreviousStartMenuEnabled = "0" OR PreviousStartMenuEnabled = "1"' After="GetStart" />
    <BootstrapperApplication>
      <bal:WixStandardBootstrapperApplication Theme="hyperlinkLargeLicense" ThemeFile="{{THEME}}" LocalizationFile="{{LOCALIZATION}}" LogoFile="{{LOGO}}" LicenseUrl="" ShowVersion="yes" LaunchTarget="[InstallFolder]\v2rayN.exe" SuppressRepair="no" SuppressOptionsUI="no" />
{{LOCALPAYLOADS}}
    </BootstrapperApplication>
    <Chain>
      <MsiPackage Id="MainMsi" SourceFile="{{MSI}}" Compressed="yes" Visible="no" Vital="yes">
        <MsiProperty Name="INSTALLFOLDER" Value="[InstallFolder]" />
        <MsiProperty Name="CREATE_DESKTOP_SHORTCUT" Value="[CreateDesktopShortcut]" />
        <MsiProperty Name="CREATE_STARTMENU_SHORTCUT" Value="[CreateStartMenuShortcut]" />
      </MsiPackage>
    </Chain>
  </Bundle>
</Wix>
'@
            foreach ($key in $templateVars.Keys) { $bundleText = $bundleText.Replace('{{' + $key + '}}', [string]$templateVars[$key]) }
            [void][System.Xml.Linq.XDocument]::Parse($bundleText)
            $bundleSource = Join-Path $work "$flavor-$arch.bundle.wxs"
            [System.IO.File]::WriteAllText($bundleSource, $bundleText, [System.Text.UTF8Encoding]::new($false))
            $archSuffix = @{ x64='64'; x86='86'; arm64='arm64' }[$arch]
            $desktopSuffix = if ($flavor -eq 'desktop') { '-desktop' } else { '' }
            $fileName = "v2rayN-windows-$archSuffix$desktopSuffix.exe"
            $setup = Join-Path $dist $fileName
            $script:Stage = "Building $flavor $arch native EXE installer"
            Run $wix @('build',$bundleSource,'-arch',$map.Wix,'-ext','WixToolset.Util.wixext','-ext','WixToolset.BootstrapperApplications.wixext','-o',$setup)
            Assert-Machine $setup $map.Machine
            Write-Host "Created: $setup"
        }
    }
} finally { Set-Location $originalLocation }
