param (
	[Parameter()]
	[ValidateNotNullOrEmpty()]
	[string]
	$OutputPath = './bin/v2rayN'
)

Write-Host 'Building'

dotnet publish `
	./v2rayN/v2rayN.csproj `
	-c Release `
	-r win-x64 `
	--self-contained false `
	-p:PublishReadyToRun=false `
	-p:PublishSingleFile=true `
	-o "$OutputPath/win-x64"

dotnet publish `
	./v2rayN/v2rayN.csproj `
	-c Release `
	-r win-arm64 `
	--self-contained false `
	-p:PublishReadyToRun=false `
	-p:PublishSingleFile=true `
	-o "$OutputPath/win-arm64"

dotnet publish `
	./v2rayN.Desktop/v2rayN.Desktop.csproj `
	-c Release `
	-r linux-x64 `
	--self-contained true `
	-p:PublishReadyToRun=false `
	-p:PublishSingleFile=true `
	-o "$OutputPath/linux-x64"
	
dotnet publish `
	./v2rayN.Desktop/v2rayN.Desktop.csproj `
	-c Release `
	-r linux-arm64 `
	--self-contained true `
	-p:PublishReadyToRun=false `
	-p:PublishSingleFile=true `
	-o "$OutputPath/linux-arm64"
 

if ( -Not $? ) {
	exit $lastExitCode
	}

if ( Test-Path -Path ./bin/v2rayN ) {
    rm -Force "$OutputPath/win-x64/*.pdb"
    rm -Force "$OutputPath/win-arm64/*.pdb"
    rm -Force "$OutputPath/linux-x64/*.pdb"
    rm -Force "$OutputPath/linux-arm64/*.pdb"
}

Write-Host 'Build done'

ls $OutputPath
7z a  v2rayN.zip $OutputPath
exit 0