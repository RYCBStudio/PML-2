@echo off
echo #################
echo ##   WARNING   ##
echo #################
echo You should delete or comment "PublishAot" and "PublishTrimmed" in .csproj file before publishing this as a linux software.
echo After you deleted or commented, press any key to continue.
pause>nul 
echo Restoring...
dotnet restore -r linux-x64
echo Restored successfully. Building...
dotnet msbuild mefrplauncherx.csproj /t:CreateDeb /p:TargetFramework=net10.0 /p:RuntimeIdentifier=linux-x64 /p:Configuration=Release /p:SelfContained=true
del /s /f /q *.pdb
echo \x1B[32mBUILD SUCCESSFUL
set /p ver=Enter the version to validate:
copy "F:\VSProj\repos\MEFrpLauncherX\bin\Release\net10.0\linux-x64\mefrplauncherx.%ver%.linux-x64.deb" "G:\VMWare\FileTransferLinux\mefrplauncherx.%ver%.linux-x64.deb"
explorer /select,"G:\VMWare\FileTransferLinux\mefrplauncherx.%ver%.linux-x64.deb"