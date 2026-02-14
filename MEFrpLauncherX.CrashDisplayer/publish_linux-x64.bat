@echo off
echo Current building: linux-x64
echo Restoring...
dotnet restore -r linux-x64
echo Restored successfully. Building...
dotnet msbuild MEFrpLauncherX.CrashDisplayer.csproj /t:CreateDeb /p:TargetFramework=net8.0 /p:RuntimeIdentifier=linux-x64 /p:Configuration=Release
echo \x1B[32mBUILD SUCCESSFUL
set /p ver=Enter the version to validate:
copy "G:\VSProj\MEFrpLauncherX.CrashDisplayer\bin\Release\net8.0\linux-x64\crashdisplyer.desktop.%ver%.linux-x64.deb" "G:\VMWare\FileTransferLinux\crashdisplyer.desktop.%ver%.linux-x64.deb"
explorer /select,"G:\VMWare\FileTransferLinux\crashdisplyer.desktop.%ver%.linux-x64.deb"