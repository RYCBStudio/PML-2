using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace MEFrpLauncherX.Tools;

public static class DesktopUtils
{
    public static async Task RestartAsync(int exitCode = 0)
    {
        // 使用独立的重启器进程（避免文件占用问题）
            if (OperatingSystem.IsWindows())
            {
                var tempBat = Path.Combine(Path.GetTempPath(), "restart.bat");
                await File.WriteAllTextAsync(tempBat, $"""

                                                       @echo off
                                                       timeout /t 1 /nobreak >nul
                                                       start "" "{Environment.ProcessPath}"
                                                       del "%~f0"
                                                       """);

                Process.Start(new ProcessStartInfo
                {
                    FileName = tempBat,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                });
            }
            else
            {
                // Linux / macOS: 使用 shell 脚本延迟重启
                var tempSh = Path.Combine(Path.GetTempPath(), $"restart-{Environment.ProcessId}.sh");
                await File.WriteAllTextAsync(tempSh, $"#!/bin/sh\nsleep 1\n\"{Environment.ProcessPath}\" >/dev/null 2>&1 &\nrm \"$0\"");
                File.SetUnixFileMode(tempSh,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

                Process.Start(new ProcessStartInfo
                {
                    FileName = "/bin/sh",
                    ArgumentList = { tempSh },
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }

            App.Desktop.Shutdown(exitCode);
    }
}