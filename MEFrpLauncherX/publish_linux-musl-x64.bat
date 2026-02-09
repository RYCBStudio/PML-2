@echo off
echo #################
echo ##   WARNING   ##
echo #################
echo You should delete or comment "PublishAot" and "PublishTrimmed" in .csproj file before publishing this as a linux software.
echo After you deleted or commented, press any key to continue.
pause>nul 
echo Current Building: linux-musl-x64
echo Restoring...
dotnet restore -r linux-musl-x64
echo Restored successfully. Building...
dotnet msbuild mefrplauncherx.csproj /t:CreateDeb /p:TargetFramework=net8.0 /p:RuntimeIdentifier=linux-musl-x64 /p:Configuration=Release
del /s /f /q *.pdb
echo \x1B[32mBUILD SUCCESSFUL
set /p ver=Enter the version to validate:
copy "F:\VSProj\repos\MEFrpLauncherX\bin\Release\net8.0\linux-musl-x64\mefrplauncherx.%ver%.linux-musl-x64.deb" "G:\VMWare\FileTransferLinux\mefrplauncherx.%ver%.linux-musl-x64.deb"
explorer /select,"G:\VMWare\FileTransferLinux\mefrplauncherx.%ver%.linux-musl-x64.deb"