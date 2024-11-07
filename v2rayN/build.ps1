param (
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $OutputPath = '.\bin\v2rayN'
)

Write-Host 'Building...'

# Publish for Windows
dotnet publish `
    .\v2rayN\v2rayN.csproj `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -p:PublishReadyToRun=false `
    -p:PublishSingleFile=true `
    -o "$OutputPath\win-x64"

# Publish for Linux
dotnet publish `
    .\v2rayN.Desktop\v2rayN.Desktop.csproj `
    -c Release `
    -r linux-x64 `
    --self-contained true `
    -p:PublishReadyToRun=false `
    -p:PublishSingleFile=true `
    -o "$OutputPath\linux-x64"

# Check if the publish succeeded
if (-Not $?) {
    exit $LASTEXITCODE
}

# Clean up PDB files if they exist
if (Test-Path -Path "$OutputPath\win-x64") {
    Remove-Item -Force "$OutputPath\win-x64\*.pdb"
}
if (Test-Path -Path "$OutputPath\linux-x64") {
    Remove-Item -Force "$OutputPath\linux-x64\*.pdb"
}

Write-Host 'Build done.'

# List the output directory contents
Get-ChildItem $OutputPath

# Create a zip archive of the output
7z a v2rayN.zip $OutputPath

exit 0
