@echo off
echo #################
echo ##   WARNING   ##
echo #################
echo You should delete or comment "PublishAot" and "PublishTrimmed" in .csproj file before publishing this as a linux software.
echo After you deleted or commented, press any key to continue.
pause>nul 
echo Current Building: linux-loongarch64
echo Restoring...
dotnet restore -r linux-loongarch64
echo Restored successfully. Building...
dotnet msbuild mefrplauncherx.csproj /t:CreateDeb /p:TargetFramework=net8.0 /p:RuntimeIdentifier=linux-loongarch64 /p:Configuration=Release
echo \x1B[32mBUILD SUCCESSFUL
set /p ver=Enter the version to validate:
copy "F:\VSProj\repos\MEFrpLauncherX\bin\Release\net8.0\linux-loongarch64\mefrplauncherx.%ver%.linux-loongarch64.deb" "G:\VMWare\FileTransferLinux\mefrplauncherx.%ver%.linux-loongarch64.deb"
explorer /select,"G:\VMWare\FileTransferLinux\mefrplauncherx.%ver%.linux-loongarch64.deb"