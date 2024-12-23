param (
	[Parameter()]
	[ValidateNotNullOrEmpty()]
	[string]
	$OutputPathWin64 = './bin/v2rayN/win-x64',
	$OutputPathWinArm64 = './bin/v2rayN/win-arm64',
)

Write-Host 'Building Windows'

dotnet publish `
	./v2rayN/v2rayN.csproj `
	-c Release `
	-r win-x64 `
	--self-contained false `
	-p:PublishReadyToRun=false `
	-p:PublishSingleFile=true `
	-o $OutputPathWin64

dotnet publish `
	./v2rayN/v2rayN.csproj `
	-c Release `
	-r win-arm64 `
	--self-contained false `
	-p:PublishReadyToRun=false `
	-p:PublishSingleFile=true `
	-o $OutputPathWinArm64

if ( -Not $? ) {
	exit $lastExitCode
	}

if ( Test-Path -Path ./bin/v2rayN ) {
    rm -Force "$OutputPathWin64/*.pdb"
    rm -Force "$OutputPathWinArm64/*.pdb"
}

Write-Host 'Build done'

7z a  v2rayN-windows-64.zip $OutputPathWin64
7z a  v2rayN-windows-arm64.zip $OutputPathWinArm64
exit 0