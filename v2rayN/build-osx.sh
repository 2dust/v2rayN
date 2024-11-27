#!/bin/sh

echo 'Building'

OutputPath='./bin/v2rayN'

dotnet publish 	./v2rayN.Desktop/v2rayN.Desktop.csproj 	-c Release 	-r osx-x64 	--self-contained true 	-p:PublishReadyToRun=false 	-p:PublishSingleFile=true 	-o "${OutputPath}/osx-x64"
dotnet publish 	./v2rayN.Desktop/v2rayN.Desktop.csproj 	-c Release 	-r osx-arm64 	--self-contained true 	-p:PublishReadyToRun=false 	-p:PublishSingleFile=true 	-o "${OutputPath}/osx-arm64"

rm -rf "$OutputPath/osx-x64/*.pdb"
rm -rf "$OutputPath/osx-arm64/*.pdb"

echo 'Build done'

ls $OutputPath
7z a  v2rayN-osx.zip $OutputPath
exit 0
