using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using PML2.Shared;

namespace PML2.Launcher;

/// <summary>
///     PML 2 启动器（安装根下的 <c>PML 2.exe</c> / <c>PML 2</c>）。
///     <para>
///         它是整个安装目录里唯一「版本无关」的部件：自身永不更新，只负责把请求转交给
///         当前生效的 <c>vXXX\MEFrpLauncherX.exe</c>。这样升级只需替换版本目录，
///         启动器与用户数据（<c>data\</c>）都不受影响。
///     </para>
/// </summary>
internal static class Program
{
    /// <summary>传递给主程序、用于锚定数据根的环境变量名。</summary>
    private const string DataRootVariable = "PML2_DATA_ROOT";

    /// <summary>传递给主程序、用于锚定安装根的环境变量名。</summary>
    private const string InstallRootVariable = "PML2_INSTALL_ROOT";

    /// <summary>安装根下的启动器自身日志（启动失败时这是唯一的诊断线索）。</summary>
    private const string LogFileName = "launcher.log";

    /// <summary>日志保留的字节上限，超出后截断，避免长期运行撑爆磁盘。</summary>
    private const long MaxLogBytes = 256 * 1024;

    [STAThread]
    private static int Main(string[] args)
    {
        var installRoot = ResolveInstallRoot();
        var parsed = ParseArguments(args);

        var resolution = VersionResolver.Resolve(installRoot, parsed.DataRoot, parsed.ExplicitVersion);

        if (!resolution.Success)
        {
            Log(installRoot, $"启动失败：{resolution.Detail}");
            ShowError(resolution.Detail, installRoot);
            return 2;
        }

        Log(installRoot, $"启动 {resolution.ExecutablePath}（{resolution.Detail}，数据根 {resolution.DataRoot}）");

        return Run(resolution, parsed.Forwarded, installRoot);
    }

    /// <summary>
    ///     拉起主程序并等待其退出。
    ///     <para>退出码透传：调用方（快捷方式、安装器、脚本）能据此判断成功与否。</para>
    /// </summary>
    private static int Run(LaunchResolution resolution, string[] args, string installRoot)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = resolution.ExecutablePath,
            UseShellExecute = false,
            WorkingDirectory = resolution.VersionFolder.Length > 0
                ? Path.Combine(installRoot, resolution.VersionFolder)
                : resolution.DataRoot
        };

        // 环境变量是主程序获取路径的唯一可靠通道：
        // AppContext.BaseDirectory 在 AOT/单文件下虽可用，但显式传递更清晰，
        // 也让「数据根」可以被安装器重定向到别处。
        startInfo.Environment[InstallRootVariable] = installRoot;
        startInfo.Environment[DataRootVariable] = resolution.DataRoot;

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        try
        {
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                Log(installRoot, "启动失败：Process.Start 返回 null");
                ShowError($"无法启动 {resolution.ExecutablePath}", installRoot);
                return 2;
            }

            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            Log(installRoot, $"启动失败：{ex}");
            ShowError($"无法启动 {resolution.ExecutablePath}\n\n{ex.Message}", installRoot);
            return 2;
        }
    }

    /// <summary>启动器解析结果。</summary>
    private readonly record struct ParsedArgs(string? DataRoot, string? ExplicitVersion, string[] Forwarded);

    /// <summary>
    ///     解析启动器自身的参数，剥离后把其余参数原样转交主程序。
    ///     <para>主程序已有 <c>pml2://</c> 与 <c>--version=</c> 风格的参数，这里只独占两个：</para>
    ///     <list type="bullet">
    ///         <item><c>--launcher-version &lt;版本&gt;</c>：强制指定要启动的 vXXX 目录。</item>
    ///         <item><c>--data &lt;路径&gt;</c>：重定向数据根（便携模式 / 测试用）。</item>
    ///     </list>
    /// </summary>
    private static ParsedArgs ParseArguments(IReadOnlyList<string> args)
    {
        string? dataRoot = null;
        string? explicitVersion = null;
        var forwarded = new List<string>(args.Count);

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];

            if (arg == "--data" && i + 1 < args.Count)
            {
                dataRoot = args[i + 1];
                i++;
                continue;
            }

            if (arg.StartsWith("--data=", StringComparison.Ordinal))
            {
                dataRoot = arg.Substring("--data=".Length);
                continue;
            }

            if (arg == "--launcher-version" && i + 1 < args.Count)
            {
                explicitVersion = args[i + 1];
                i++;
                continue;
            }

            if (arg.StartsWith("--launcher-version=", StringComparison.Ordinal))
            {
                explicitVersion = arg.Substring("--launcher-version=".Length);
                continue;
            }

            forwarded.Add(arg);
        }

        return new ParsedArgs(dataRoot, explicitVersion, forwarded.ToArray());
    }

    /// <summary>
    ///     推导安装根：启动器可执行文件所在目录。
    ///     <para>AOT / 单文件下 <c>Assembly.Location</c> 为空，必须用 <c>ProcessPath</c>。</para>
    /// </summary>
    private static string ResolveInstallRoot()
    {
        var executable = Environment.ProcessPath;
        var directory = string.IsNullOrEmpty(executable)
            ? AppContext.BaseDirectory
            : Path.GetDirectoryName(executable)!;

        return Path.GetFullPath(string.IsNullOrEmpty(directory) ? Directory.GetCurrentDirectory() : directory);
    }

    /// <summary>
    ///     写入启动器日志。启动器没有 UI 也没有 Avalonia，日志是失败时唯一的诊断手段，
    ///     因此写入失败必须静默（不能因日志不可写而阻止启动）。
    /// </summary>
    private static void Log(string installRoot, string message)
    {
        try
        {
            var logDirectory = Path.Combine(installRoot, "data", "Logs");
            Directory.CreateDirectory(logDirectory);

            var path = Path.Combine(logDirectory, LogFileName);
            Trim(path);

            File.AppendAllText(path,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
            // 日志不可写不应影响启动。
        }
    }

    /// <summary>日志超过上限时清空重来，避免无限增长。</summary>
    private static void Trim(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.Exists && info.Length > MaxLogBytes)
            {
                File.Delete(path);
            }
        }
        catch
        {
            // 同上，忽略。
        }
    }

    /// <summary>以原生弹窗报告启动失败（GUI 程序无控制台可用）。</summary>
    private static void ShowError(string message, string installRoot)
    {
        try
        {
            NativeMessageBox.Show(
                $"{message}\n\n安装目录：{installRoot}\n\n" +
                "请尝试重新安装 PML 2；若问题持续，可将安装目录下的 data\\Logs\\launcher.log 一并反馈。",
                "PML 2 启动失败");
        }
        catch
        {
            // 连弹窗都失败时无处可报，只能依赖日志。
        }
    }
}

/// <summary>Win32 MessageBoxW 的最小 P/Invoke。</summary>
internal static class NativeMessageBox
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBoxW(IntPtr hWnd, string lpText, string lpCaption, uint uType);

    /// <summary>MB_OK | MB_ICONERROR。</summary>
    private const uint MbOkIconError = 0x00000010 | 0x00000002;

    internal static void Show(string text, string caption)
    {
        if (OperatingSystem.IsWindows())
        {
            MessageBoxW(IntPtr.Zero, text, caption, MbOkIconError);
        }
    }
}
