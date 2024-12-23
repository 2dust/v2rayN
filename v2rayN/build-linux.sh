#!/bin/sh

echo 'Building Linux'

OutputPath='./bin/v2rayN'
OutputPath64="${OutputPath}/linux-x64"
OutputPathArm64="${OutputPath}/linux-arm64"

dotnet publish 	./v2rayN.Desktop/v2rayN.Desktop.csproj 	-c Release 	-r linux-x64 	--self-contained true 	-p:PublishReadyToRun=false 	-p:PublishSingleFile=true 	-o $OutputPath64
dotnet publish 	./v2rayN.Desktop/v2rayN.Desktop.csproj 	-c Release 	-r linux-arm64 	--self-contained true 	-p:PublishReadyToRun=false 	-p:PublishSingleFile=true 	-o $OutputPathArm64

rm -rf "$OutputPath64/*.pdb"
rm -rf "$OutputPathArm64/*.pdb"

echo 'Build done'

7z a  v2rayN-linux.zip $OutputPath
exit 0
