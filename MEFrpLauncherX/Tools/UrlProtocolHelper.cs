using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MEFrpLauncherX.Tools;
#pragma warning disable CA1416

public static class UrlProtocolHelper
{
    private const string ProtocolName = "pml2";
    private const string ProtocolDescription = "PML2 Protocol";

    /// <summary>
    ///     确保系统把 <c>pml2://</c> 交回本程序处理（幂等，失败不影响启动）。
    ///     Windows 写 <c>HKCU\Software\Classes</c>；Linux 写 xdg desktop 文件并注册为默认处理器；
    ///     macOS 由应用包（Info.plist 的 CFBundleURLTypes）声明，这里什么都不做。
    /// </summary>
    /// <param name="appPath">当前可执行文件路径（AOT/单文件下请传 Environment.ProcessPath）</param>
    /// <returns>已完成注册（或当前平台无需注册）返回 true。</returns>
    public static bool EnsureRegistered(string? appPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(appPath))
            {
                return false;
            }

            // macOS：由 .app 包的 CFBundleURLTypes 声明，运行时无需写任何东西
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return true;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (string.Equals(ReadWindowsCommand(), BuildWindowsCommand(appPath), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                RegisterWindows(appPath);
                Core.App.CurrentLogger?.Info($"[{ProtocolName}://] 已注册 → {appPath}");
                return true;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (IsLinuxDesktopEntryCurrent(appPath))
                {
                    return true;
                }

                RegisterLinux(appPath);
                Core.App.CurrentLogger?.Info($"[{ProtocolName}://] 已注册 → {appPath}");
                return true;
            }
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Warning($"[{ProtocolName}://] 协议注册失败：{ex.Message}");
        }

        return false;
    }

    /// <summary>手动注册协议（Windows / Linux 有效；macOS 会抛 PlatformNotSupportedException）。</summary>
    public static void RegisterProtocol(string appPath)
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

        Core.App.CurrentLogger?.Info($"[{ProtocolName}://] registered → {appPath}");
    }

    /// <summary>解除协议注册（Windows / Linux；macOS 需用户自行移除应用关联）。</summary>
    public static void UnregisterProtocol()
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

        /// <summary>注册表里应写入的 open 命令（幂等比较用）</summary>
        private static string BuildWindowsCommand(string appPath) => $"\"{appPath}\" \"%1\"";

        /// <summary>读取当前已注册的 open 命令（未注册时返回 null）。</summary>
        private static string? ReadWindowsCommand()
        {
            using var commandKey = Registry.CurrentUser.OpenSubKey(
                $@"Software\Classes\{ProtocolName}\shell\open\command");
            return commandKey?.GetValue(string.Empty) as string;
        }

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
                commandKey.SetValue("", BuildWindowsCommand(appPath));
            }
        }

        static void UnregisterWindows()
        {
            Registry.CurrentUser.DeleteSubKeyTree(
                $@"Software\Classes\{ProtocolName}", throwOnMissingSubKey: false);
        }

        // ==================== Linux ====================

        /// <summary>Linux 下写入的 desktop 文件名</summary>
        private static string LinuxDesktopFileName => $"{ProtocolName}-handler.desktop";

        /// <summary>用户级 applications 目录</summary>
        private static string LinuxApplicationsDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "applications");

        /// <summary>desktop 文件完整路径</summary>
        private static string LinuxDesktopFilePath => Path.Combine(LinuxApplicationsDir, LinuxDesktopFileName);

        /// <summary>desktop 文件内容（Exec 指向当前程序，%u 传入链接）。</summary>
        private static string BuildLinuxDesktopContent(string appPath) => $@"[Desktop Entry]
Type=Application
Name={ProtocolDescription} Handler
Exec=""{appPath}"" %u
StartupNotify=false
MimeType=x-scheme-handler/{ProtocolName};
NoDisplay=true
Terminal=false
";

        /// <summary>现有 desktop 文件是否已指向当前程序（幂等判断）。</summary>
        private static bool IsLinuxDesktopEntryCurrent(string appPath)
        {
            try
            {
                var path = LinuxDesktopFilePath;
                return File.Exists(path) &&
                       string.Equals(File.ReadAllText(path), BuildLinuxDesktopContent(appPath),
                           StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        static void RegisterLinux(string appPath)
        {
            string desktopFileName = LinuxDesktopFileName;
            string applicationsDir = LinuxApplicationsDir;
            Directory.CreateDirectory(applicationsDir);
            string desktopFilePath = LinuxDesktopFilePath;

            File.WriteAllText(desktopFilePath, BuildLinuxDesktopContent(appPath));

            // 更新 MIME 数据库
            RunCommand("update-desktop-database", applicationsDir);

            // 注册为默认处理器
            RunCommand("xdg-mime", $"default {desktopFileName} x-scheme-handler/{ProtocolName}");

            Core.App.CurrentLogger?.Info($"Desktop file written to: {desktopFilePath}");
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
