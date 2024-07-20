param (
	[Parameter()]
	[ValidateNotNullOrEmpty()]
	[string]
	$OutputPath = '.\bin\v2rayMiniConsole'
)

Write-Host 'Building'

dotnet publish `
	.\v2rayMiniConsole\v2rayMiniConsole.csproj `
	-c Release `
	--self-contained false `
	-p:PublishReadyToRun=true `
	-p:PublishSingleFile=true `
	-o $OutputPath

dotnet publish `
	.\v2rayUpgrade\v2rayUpgrade.csproj `
	-c Release `
	--self-contained false `
	-p:PublishReadyToRun=true `
	-p:PublishSingleFile=true `
	-o $OutputPath

if ( -Not $? ) {
	exit $lastExitCode
	}

if ( Test-Path -Path .\bin\v2rayMiniConsole ) {
    rm -Force "$OutputPath\*.pdb"
    rm -Force "$OutputPath\*.xml"
}

Write-Host 'Build done'

ls $OutputPath
7z.exe a  v2rayMiniConsole.zip $OutputPath
exit 0