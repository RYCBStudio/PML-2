using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MEFrpLauncherX.Tools;
#pragma warning disable CA1416

public class UrlProtocolHelper
{
    private const string ProtocolName = "pml2";
        private const string ProtocolDescription = "PML2 Protocol";

        static void RegisterProtocol(string appPath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                RegisterWindows(appPath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                RegisterLinux(appPath);
            }
            else
            {
                throw new PlatformNotSupportedException("Unsupported operating system.");
            }

            Core.App.CurrentLogger.Info($"[{ProtocolName}://] registered → {appPath}");
        }

        static void UnregisterProtocol()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                UnregisterWindows();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                UnregisterLinux();
            }

            Core.App.CurrentLogger.Info($"[{ProtocolName}://] unregistered.");
        }

        // ==================== Windows ====================
        static void RegisterWindows(string appPath)
        {
            // HKCU\Software\Classes\pml2
            using (RegistryKey protocolKey = Registry.CurrentUser.CreateSubKey(
                $@"Software\Classes\{ProtocolName}"))
            {
                protocolKey.SetValue("", $"URL:{ProtocolName} Protocol");
                protocolKey.SetValue("URL Protocol", "");
            }

            // HKCU\Software\Classes\pml2\shell\open\command
            using (RegistryKey commandKey = Registry.CurrentUser.CreateSubKey(
                $@"Software\Classes\{ProtocolName}\shell\open\command"))
            {
                commandKey.SetValue("", $"\"{appPath}\" \"%1\"");
            }
        }

        static void UnregisterWindows()
        {
            Registry.CurrentUser.DeleteSubKeyTree(
                $@"Software\Classes\{ProtocolName}", throwOnMissingSubKey: false);
        }

        // ==================== Linux ====================
        static void RegisterLinux(string appPath)
        {
            string desktopFileName = $"{ProtocolName}-handler.desktop";
            string applicationsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "share", "applications");
            Directory.CreateDirectory(applicationsDir);
            string desktopFilePath = Path.Combine(applicationsDir, desktopFileName);

            // 桌面文件内容
            string desktopContent = $@"[Desktop Entry]
Type=Application
Name={ProtocolDescription} Handler
Exec=""{appPath}"" %u
StartupNotify=false
MimeType=x-scheme-handler/{ProtocolName};
NoDisplay=true
Terminal=false
";

            File.WriteAllText(desktopFilePath, desktopContent);

            // 更新 MIME 数据库
            RunCommand("update-desktop-database", applicationsDir);

            // 注册为默认处理器
            RunCommand("xdg-mime", $"default {desktopFileName} x-scheme-handler/{ProtocolName}");

            Core.App.CurrentLogger.Info($"Desktop file written to: {desktopFilePath}");
        }

        static void UnregisterLinux()
        {
            string desktopFileName = $"{ProtocolName}-handler.desktop";
            string applicationsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "share", "applications");
            string desktopFilePath = Path.Combine(applicationsDir, desktopFileName);

            if (File.Exists(desktopFilePath))
                File.Delete(desktopFilePath);

            RunCommand("update-desktop-database", applicationsDir);

            // 从 mimeapps.list 中移除关联（可选）
            string mimeAppsList = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config", "mimeapps.list");
            if (File.Exists(mimeAppsList))
            {
                string[] lines = File.ReadAllLines(mimeAppsList);
                var output = lines.Where(l => 
                    !l.StartsWith($"x-scheme-handler/{ProtocolName}=")).ToArray();
                File.WriteAllLines(mimeAppsList, output);
            }
        }

        // ==================== 工具方法 ====================
        static void RunCommand(string command, string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                process.WaitForExit(5000);

                if (process.ExitCode != 0)
                {
                    string err = process.StandardError.ReadToEnd();
                    Core.App.CurrentLogger.Error($"Warning: {command} exited with code {process.ExitCode}: {err}");
                }
            }
            catch (Exception ex)
            {
                Core.App.CurrentLogger.Error($"Warning: failed to run '{command} {args}': {ex.Message}");
            }
        }
    }
