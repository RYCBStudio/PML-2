using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
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
    ///     定位主程序可执行文件。发布布局下崩溃报告器位于 &lt;AppRoot&gt;/Tools/，主程序位于 &lt;AppRoot&gt;/。
    /// </summary>
    private static string? LocateMainExecutable()
    {
        try
        {
            var exeName = OperatingSystem.IsWindows() ? "MEFrpLauncherX.exe" : "MEFrpLauncherX";
            var baseDir = AppContext.BaseDirectory;

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
                var appRoot = Path.GetDirectoryName(_mainExePath);
                var logs = Path.Combine(appRoot ?? "", "Logs");
                if (Directory.Exists(logs))
                {
                    return logs;
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
                var appRoot = Path.GetDirectoryName(_mainExePath);
                var config = Path.Combine(appRoot ?? "", "Config");
                if (Directory.Exists(config))
                {
                    return config;
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
