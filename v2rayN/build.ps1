param (
	[Parameter()]
	[ValidateNotNullOrEmpty()]
	[string]
	$OutputPathWin64 = './bin/v2rayN/win-x64',
	$OutputPathWinArm64 = './bin/v2rayN/win-arm64',
	$OutputPathLinux64 = './bin/v2rayN/linux-x64',
	$OutputPathLinuxArm64 = './bin/v2rayN/linux-arm64'
)

Write-Host 'Building'

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

dotnet publish `
	./v2rayN.Desktop/v2rayN.Desktop.csproj `
	-c Release `
	-r linux-x64 `
	--self-contained true `
	-p:PublishReadyToRun=false `
	-p:PublishSingleFile=true `
	-o $OutputPathLinux64
	
dotnet publish `
	./v2rayN.Desktop/v2rayN.Desktop.csproj `
	-c Release `
	-r linux-arm64 `
	--self-contained true `
	-p:PublishReadyToRun=false `
	-p:PublishSingleFile=true `
	-o $OutputPathLinuxArm64
 

if ( -Not $? ) {
	exit $lastExitCode
	}

if ( Test-Path -Path ./bin/v2rayN ) {
    rm -Force "$OutputPathWin64/*.pdb"
    rm -Force "$OutputPathWinArm64/*.pdb"
    rm -Force "$OutputPathLinux64/*.pdb"
    rm -Force "$OutputPathLinuxArm64/*.pdb"
}

Write-Host 'Build done'

7z a  v2rayN-windows-64.zip $OutputPathWin64
7z a  v2rayN-windows-arm64.zip $OutputPathWinArm64
7z a  v2rayN-linux-64.zip $OutputPathLinux64
7z a  v2rayN-linux-arm64.zip $OutputPathLinuxArm64
exit 0