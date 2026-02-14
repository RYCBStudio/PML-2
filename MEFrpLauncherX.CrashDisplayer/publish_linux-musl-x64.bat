@echo off
echo Current Building: linux-musl-x64
echo Restoring...
dotnet restore -r linux-musl-x64
echo Restored successfully. Building...
dotnet msbuild MEFrpLauncherX.CrashDisplayer.csproj /t:CreateDeb /p:TargetFramework=net8.0 /p:RuntimeIdentifier=linux-musl-x64 /p:Configuration=Release
echo \x1B[32mBUILD SUCCESSFUL
set /p ver=Enter the version to validate:
copy "G:\VSProj\MEFrpLauncherX.CrashDisplayer\bin\Release\net8.0\linux-musl-x64\crashdisplyer.desktop.%ver%.linux-musl-x64.deb" "G:\VMWare\FileTransferLinux\crashdisplyer.desktop.%ver%.linux-musl-x64.deb"
explorer /select,"G:\VMWare\FileTransferLinux\crashdisplyer.desktop.%ver%.linux-musl-x64.deb"