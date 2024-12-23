#!/bin/sh

echo 'Building'

OutputPath='./bin/v2rayN/osx-x64'
OutputPathArm64='./bin/v2rayN/osx-arm64'

dotnet publish 	./v2rayN.Desktop/v2rayN.Desktop.csproj 	-c Release 	-r osx-x64 	--self-contained true 	-p:PublishReadyToRun=false 	-p:PublishSingleFile=true 	-o $OutputPath
dotnet publish 	./v2rayN.Desktop/v2rayN.Desktop.csproj 	-c Release 	-r osx-arm64 	--self-contained true 	-p:PublishReadyToRun=false 	-p:PublishSingleFile=true 	-o $OutputPathArm64

rm -rf "$OutputPath/*.pdb"
rm -rf "$OutputPathArm64/*.pdb"

echo 'Build done'

7z a  v2rayN-macos-64.zip $OutputPath
7z a  v2rayN-macos-arm64.zip $OutputPathArm64
exit 0
