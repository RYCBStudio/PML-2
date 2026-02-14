@echo off
echo Current building: win-x64
echo Restoring...
dotnet restore -r win-x64
echo Restored successfully. Building...
dotnet msbuild MEFrpLauncherX.CrashDisplayer.csproj /p:TargetFramework=net8.0 /p:RuntimeIdentifier=win-x64 /p:Configuration=Release
echo \x1B[32mBUILD SUCCESSFUL
set /p ver=Enter the version to validate:
explorer /select,"G:\VSProj\MEFrpLauncherX.CrashDisplayer\bin\Release\net8.0\win-x64"