param (
	[Parameter()]
	[ValidateNotNullOrEmpty()]
	[string]
	$OutputPath = '.\bin\v2rayN'
)

Write-Host 'Building'

dotnet publish `
	.\v2rayN\v2rayN.csproj `
	-c Release `
	--self-contained false `
	-p:PublishReadyToRun=false `
	-p:PublishSingleFile=true `
	-o $OutputPath

dotnet publish `
	.\AmazTool\AmazTool.csproj `
	-c Release `
	--self-contained false `
	-p:PublishReadyToRun=false `
	-p:PublishSingleFile=true `
	-o $OutputPath

if ( -Not $? ) {
	exit $lastExitCode
	}

if ( Test-Path -Path .\bin\v2rayN ) {
    rm -Force "$OutputPath\*.pdb"
    rm -Force "$OutputPath\*.xml"
}

Write-Host 'Build done'

ls $OutputPath
7z a  v2rayN.zip $OutputPath
exit 0