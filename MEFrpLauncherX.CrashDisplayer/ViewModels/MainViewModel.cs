using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
// Avalonia 12：IClipboard.SetTextAsync 改为扩展方法（ClipboardExtensions），需导入此命名空间。
using Avalonia.Input.Platform;
using MEFrpLauncherX.CrashDisplayer.Models;

namespace MEFrpLauncherX.CrashDisplayer.ViewModels;

public partial class MainViewModel
{
    private const string IssueBaseUrl = "https://github.com/RYCBStudio/PML-2/issues/new";

    private readonly string? _logFilePath;
    private readonly string? _mainExePath;

    public string JokeMessage { get; }
    public string HeaderSubtitle { get; }
    public string ErrorSummary { get; }
    public string ErrorDetails { get; }
    public ErrorDiagnosis Diagnosis { get; }
    public bool CanRestart { get; }
    public ICommand CopyCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand RestartCommand { get; }
    public ICommand ReportCommand { get; }
    public ICommand SolutionActionCommand { get; }

    public static string OopsCrashed => CrashStrings.OopsCrashed;
    public static string ProgrammerHumor => CrashStrings.ProgrammerHumor;
    public static string HumorNote => CrashStrings.HumorNote;
    public static string ErrorDetailsLabel => CrashStrings.ErrorDetailsLabel;
    public static string CopyErrorInfo => CrashStrings.CopyErrorInfo;
    public static string Close => CrashStrings.Close;
    public static string DiagnosisTitle => CrashStrings.DiagnosisTitle;
    public static string SolutionsTitle => CrashStrings.SolutionsTitle;
    public static string RestartApplication => CrashStrings.RestartApplication;
    public static string OpenLogDirectory => CrashStrings.OpenLogDirectory;
    public static string ReportIssue => CrashStrings.ReportIssue;

    /// <summary>设计时/兜底构造：使用占位崩溃信息，保证界面始终可展示。</summary>
    public MainViewModel() : this("", "")
    {
    }

    public MainViewModel(string exJson, string crashLogArg)
    {
        // 幽默消息
        var jokes = CrashStrings.Jokes;
        JokeMessage = jokes[Random.Shared.Next(jokes.Length)];

        // 防御性解析崩溃负载（任何字段损坏都回退为占位值）
        var report = CrashPayloadParser.Parse(exJson, crashLogArg);
        var ex = report.Exception;
        if (ex.Type.Contains("QuicException"))
        {
            Environment.Exit(0);
        }

        _logFilePath = report.LogFilePath;
        _mainExePath = LocateMainExecutable();

        // 智能诊断：把原始异常翻译为「错误原因 + 解决方案」
        Diagnosis = ErrorDiagnosisEngine.Diagnose(ex.Type, ex.Message);

        HeaderSubtitle = $"{CrashStrings.ErrorTypeLabel}: {ex.Type}";
        ErrorSummary = $"{CrashStrings.ErrorMessageLabel}: {ex.Message}";
        ErrorDetails = report.FullLog;

        // 命令
        var detailsCopy = ErrorDetails;
        CopyCommand = new RelayCommand(_ =>
        {
            try
            {
                (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
                    ?.MainWindow?.Clipboard?.SetTextAsync(detailsCopy);
            }
            catch
            {
                // 剪贴板不可用时静默失败
            }
        });
        CloseCommand = new RelayCommand(_ =>
            (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown());

        CanRestart = _mainExePath is not null;
        var mainExe = _mainExePath;
        RestartCommand = new RelayCommand(_ => RestartApplicationImpl(mainExe));
        ReportCommand = new RelayCommand(_ => ReportIssueImpl(ex));
        SolutionActionCommand = new RelayCommand(param =>
        {
            if (param is SolutionAction action)
            {
                ExecuteSolutionAction(action, mainExe);
            }
        });
    }

    private void ExecuteSolutionAction(SolutionAction action, string? mainExe)
    {
        switch (action)
        {
            case SolutionAction.OpenLogDirectory:
                OpenFolder(ResolveLogsDirectory());
                break;
            case SolutionAction.OpenConfigDirectory:
                OpenFolder(ResolveConfigDirectory());
                break;
            case SolutionAction.RestartApplication:
                RestartApplicationImpl(mainExe);
                break;
            case SolutionAction.ReportIssue:
                ReportCommand.Execute(null);
                break;
        }
    }

    private void ReportIssueImpl(ExceptionPayload ex)
    {
        try
        {
            var title = Uri.EscapeDataString($"[Crash] {ex.Type}: {Truncate(ex.Message, 80)}");
            var body = Uri.EscapeDataString(Truncate(
                $"## Crash Report{Environment.NewLine}{Environment.NewLine}```{Environment.NewLine}{ErrorDetails}{Environment.NewLine}```",
                4000));
            Process.Start(new ProcessStartInfo($"{IssueBaseUrl}?title={title}&body={body}")
            {
                UseShellExecute = true
            });
        }
        catch
        {
            // 浏览器不可用时静默失败，错误信息仍可通过复制按钮获取
        }
    }

    private void RestartApplicationImpl(string? mainExe)
    {
        try
        {
            if (mainExe is null)
            {
                return;
            }

            Process.Start(new ProcessStartInfo(mainExe) { UseShellExecute = true });
            CloseCommand.Execute(null);
        }
        catch
        {
            // 重启失败时保持当前窗口，用户仍可手动操作
        }
    }

    /// <summary>
    ///     定位主程序可执行文件。
    ///     <para>
    ///         26.5.0 起布局为 <c>&lt;安装根&gt;/vXXX/MEFrpLauncherX.exe</c> + <c>&lt;安装根&gt;/data/Run/</c>，
    ///         崩溃报告器位于 <c>&lt;安装根&gt;/data/Run/</c>，因此需向上两级回到安装根，
    ///         再扫描版本目录（版本号最大者优先）。
    ///     </para>
    ///     <para>旧版布局（报告器在主程序旁的 Tools/）保留为回退分支。</para>
    /// </summary>
    private static string? LocateMainExecutable()
    {
        try
        {
            var exeName = OperatingSystem.IsWindows() ? "MEFrpLauncherX.exe" : "MEFrpLauncherX";
            var baseDir = AppContext.BaseDirectory;

            // 新版：data\Run\ → 安装根；再从 launcher.json 或扫描取版本目录
            var installRoot = Path.GetFullPath(Path.Combine(baseDir, "..", ".."));
            foreach (var candidate in EnumerateCandidatePaths(installRoot, exeName))
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            // 旧版布局：报告器与主程序同目录，或位于其下 Tools\
            var publishLayout = Path.GetFullPath(Path.Combine(baseDir, "..", exeName));
            if (File.Exists(publishLayout))
            {
                return publishLayout;
            }

            var sameDir = Path.Combine(baseDir, exeName);
            if (File.Exists(sameDir))
            {
                return sameDir;
            }
        }
        catch
        {
            // 定位失败仅意味着「重启应用」按钮不可用
        }

        return null;
    }

    /// <summary>枚举 <paramref name="installRoot" /> 下可能的主程序路径（launcher.json 优先，其次版本号降序）。</summary>
    private static IEnumerable<string> EnumerateCandidatePaths(string installRoot, string exeName)
    {
        // 1. launcher.json 的 current 字段最权威
        var manifest = Path.Combine(installRoot, "launcher.json");
        if (File.Exists(manifest))
        {
            string? current = null;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifest));
                if (doc.RootElement.TryGetProperty("current", out var value))
                {
                    current = value.GetString();
                }
            }
            catch
            {
                // manifest 损坏时回退到扫描
            }

            if (!string.IsNullOrEmpty(current))
            {
                yield return Path.Combine(installRoot, current, exeName);
            }
        }

        // 2. 扫描 vXXX 目录，按版本号降序
        if (!Directory.Exists(installRoot))
        {
            yield break;
        }

        foreach (var dir in Directory.EnumerateDirectories(installRoot)
                     .Select(Path.GetFileName)
                     .Where(name => !string.IsNullOrEmpty(name) && name.Length > 1 &&
                                    (name[0] == 'v' || name[0] == 'V') && char.IsDigit(name[1]))
                     .OrderByDescending(name => name, StringComparer.OrdinalIgnoreCase))
        {
            yield return Path.Combine(installRoot, dir!, exeName);
        }

        // 3. 开发态：主程序就在安装根下
        yield return Path.Combine(installRoot, exeName);
    }

    /// <summary>推导日志目录：优先根据负载文件路径（Logs/Crash/*.log → Logs），其次按主程序目录推导。</summary>
    private string ResolveLogsDirectory()
    {
        try
        {
            if (_logFilePath is not null)
            {
                var crashDir = Path.GetDirectoryName(_logFilePath);
                var logsDir = Path.GetDirectoryName(crashDir ?? "");
                if (!string.IsNullOrEmpty(logsDir) && Directory.Exists(logsDir))
                {
                    return logsDir;
                }

                if (!string.IsNullOrEmpty(crashDir) && Directory.Exists(crashDir))
                {
                    return crashDir;
                }
            }

            if (_mainExePath is not null)
            {
                // 26.5.0：主程序位于 <安装根>/vXXX/，日志/配置在 <安装根>/data/ 下。
                var versionDir = Path.GetDirectoryName(_mainExePath);
                var installRoot = Path.GetDirectoryName(versionDir ?? "");
                var dataRoot = Path.Combine(installRoot ?? "", "data");

                var logs = Path.Combine(dataRoot, "Logs");
                if (Directory.Exists(logs))
                {
                    return logs;
                }

                // 旧版单层布局回退：日志就在主程序目录下的 Logs\
                var legacyLogs = Path.Combine(versionDir ?? "", "Logs");
                if (Directory.Exists(legacyLogs))
                {
                    return legacyLogs;
                }
            }

            var baseLogs = Path.Combine(AppContext.BaseDirectory, "Logs");
            if (Directory.Exists(baseLogs))
            {
                return baseLogs;
            }
        }
        catch
        {
            // 回退到程序目录
        }

        return AppContext.BaseDirectory;
    }

    private string ResolveConfigDirectory()
    {
        try
        {
            if (_mainExePath is not null)
            {
                // 26.5.0：配置在 <安装根>/data/Config/；旧版布局在主程序目录下的 Config\。
                var versionDir = Path.GetDirectoryName(_mainExePath);
                var installRoot = Path.GetDirectoryName(versionDir ?? "");

                var config = Path.Combine(installRoot ?? "", "data", "Config");
                if (Directory.Exists(config))
                {
                    return config;
                }

                var legacyConfig = Path.Combine(versionDir ?? "", "Config");
                if (Directory.Exists(legacyConfig))
                {
                    return legacyConfig;
                }
            }

            var baseConfig = Path.Combine(AppContext.BaseDirectory, "Config");
            if (Directory.Exists(baseConfig))
            {
                return baseConfig;
            }
        }
        catch
        {
            // 回退到程序目录
        }

        return AppContext.BaseDirectory;
    }

    private static void OpenFolder(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", $"\"{path}\"");
            }
            else
            {
                Process.Start("xdg-open", $"\"{path}\"");
            }
        }
        catch
        {
            // 文件管理器不可用时静默失败
        }
    }

    private static string Truncate(string? text, int maxLength) =>
        text is null || text.Length <= maxLength ? text ?? "" : text[..maxLength];
}

public class RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    : ICommand
{
    private readonly Action<object?> _execute = execute ?? throw new ArgumentNullException(nameof(execute));

    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

#pragma warning disable CS0067 // ICommand 接口要求声明该事件，当前实现无需触发
    public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067
}
