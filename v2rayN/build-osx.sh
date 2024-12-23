#!/bin/sh

echo 'Building macOS'

OutputPath='./bin/v2rayN'
OutputPath64="${OutputPath}/osx-x64"
OutputPathArm64="${OutputPath}/osx-arm64"

dotnet publish 	./v2rayN.Desktop/v2rayN.Desktop.csproj 	-c Release 	-r osx-x64 	--self-contained true 	-p:PublishReadyToRun=false 	-p:PublishSingleFile=true 	-o $OutputPath64
dotnet publish 	./v2rayN.Desktop/v2rayN.Desktop.csproj 	-c Release 	-r osx-arm64 	--self-contained true 	-p:PublishReadyToRun=false 	-p:PublishSingleFile=true 	-o $OutputPathArm64

rm -rf "$OutputPath64/*.pdb"
rm -rf "$OutputPathArm64/*.pdb"

echo 'Build done'

7z a  v2rayN-osx.zip $OutputPath
exit 0
