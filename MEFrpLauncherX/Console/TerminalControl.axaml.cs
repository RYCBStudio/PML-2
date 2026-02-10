using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using FluentAvalonia.UI.Controls;
using MEFrpLauncherX.Controls;
using MEFrpLauncherX.Core;
using RYCB.PML2.Mixin.TerminalHelper;

// ReSharper disable InconsistentNaming

// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedParameter.Local
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
// ReSharper disable PossibleUnintendedReferenceComparison
#pragma warning disable CS8625 // 无法将 null 字面量转换为非 null 的引用类型。
#pragma warning disable CS8622 // 参数类型中引用类型的为 Null 性与目标委托不匹配(可能是由于为 Null 性特性)。

namespace MEFrpLauncherX.Console;

public partial class TerminalControl : UserControl, IDisposable
{
    private bool _ctrlPressed;
    private readonly List<string> _history = [];
    private int _historyIndex;
    private string _shell;
    private Process _process;
    private StreamWriter _inputWriter;
    private Thread _outputReaderThread;
    private bool _disposed;
    private StringBuilder _outputBuffer = new();
    private readonly object _bufferLock = new();
    private readonly AnsiColorizingTransformer _colorizer = new();
    private TunnelErrorInfoShell _tunnelErrorInfoShell;

    public TerminalControl()
    {
        InitializeComponent();
        InitializeAvaloniaEdit();
        OutputBox.TextArea.TextView.LineTransformers.Add(_colorizer);
        InputBox.KeyDown += InputBox_KeyDown;
        InputBox.KeyUp += InputBox_KeyUp;

        // Start with default shell based on platform
        StartTerminal(GetDefaultShell());
    }

    private void InitializeAvaloniaEdit()
    {
        // 加载语法高亮定义
        //OutputBox.SyntaxHighlighting = LoadHighlightingDefinition();

        // 配置编辑器选项
        OutputBox.Options = new TextEditorOptions
        {
            ShowBoxForControlCharacters = false, // 重要：禁用控制字符显示
            EnableHyperlinks = false,
            EnableEmailHyperlinks = false,
            EnableVirtualSpace = false,
            HighlightCurrentLine = false,
            AllowScrollBelowDocument = true
        };

        // 设置只读
        OutputBox.IsReadOnly = true;
    }

    private IHighlightingDefinition LoadHighlightingDefinition()
    {
        try
        {
            // 方法1: 使用 Avalonia 的资源加载器
            using var reader =
                new XmlTextReader(AssetLoader.Open(new Uri("avares://MEFrpLauncherX/Assets/Terminal.xshd")));
            return HighlightingLoader.Load(reader, HighlightingManager.Instance);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"通过AvaloniaResource加载语法高亮失败: {ex.Message}");

            // 备用方法: 尝试从文件系统加载
            return LoadHighlightingFromFileSystem();
        }
    }

    private IHighlightingDefinition LoadHighlightingFromFileSystem()
    {
        try
        {
            // 检查常见位置
            var possiblePaths = new[]
            {
                "Resources/Terminal.xshd",
                "Assets/Terminal.xshd",
                "Terminal.xshd",
                Path.Combine(AppContext.BaseDirectory, "Assets", "Terminal.xshd"),
                Path.Combine(AppContext.BaseDirectory, "Terminal.xshd")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    using var reader = new XmlTextReader(path);
                    return HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
            }

            Debug.WriteLine("未找到语法高亮文件");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"从文件系统加载语法高亮失败: {ex.Message}");
            return null;
        }
    }

    public static string GetDefaultShellBak()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "powershell.exe";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return GetMacOSDefaultShell();
        }
        else
        {
            return "/bin/bash";
        }
    }

    private static string GetMacOSDefaultShell()
    {
        try
        {
            // 首先使用环境变量 SHELL（通常在 macOS 上设置为用户默认 shell）
            var shell = Environment.GetEnvironmentVariable("SHELL");
            if (!string.IsNullOrEmpty(shell) && File.Exists(shell))
            {
                return shell;
            }

            // 备用常见路径（zsh 是 Catalina 及以后的默认）
            var commonShells = new[] { "/bin/zsh", "/bin/bash", "/bin/sh" };
            foreach (var shellPath in commonShells)
            {
                if (File.Exists(shellPath))
                {
                    return shellPath;
                }
            }

            return "/bin/bash";
        }
        catch
        {
            return "/bin/bash";
        }
    }

    // 新增取消令牌（用于终止异步读取任务）
    private CancellationTokenSource _cts;

// 重构 StartTerminal 方法
    public void StartTerminal(string? shell = null)
    {
        LogDebug("开始启动终端");
        // 1. 清理旧资源（关键：先取消旧任务）
        DisposeProcess();

        _disposed = false;
        _cts = new CancellationTokenSource(); // 新建取消令牌

        // 2. 确定 Shell（Linux 优先用 sh，避免 bash/zsh 交互阻塞）
        _shell = string.IsNullOrEmpty(shell) ? GetDefaultShell() : shell;
        // Linux 强制替换为 sh（非交互模式），避免 bash -i 卡死
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _shell = "/bin/sh"; // 替换为最基础的 sh，避免交互模式问题
            LogDebug($"Linux 环境强制使用 Shell: {_shell}");
        }

        // 3. 初始化进程
        _process = new Process();
        ConfigureProcessStartInfo(); // 抽离进程配置

        try
        {
            LogDebug("启动进程...");
            _process.Start();
            LogDebug("进程启动成功");
            _inputWriter = _process.StandardInput;

            // 4. 启动异步读取任务（替换同步 Thread）
            _ = ReadOutputAsync(_cts.Token);
            _ = ReadErrorAsync(_cts.Token);

            // 5. 进程退出事件
            _process.Exited += (s, e) =>
            {
                LogDebug($"进程退出，退出码: {_process.ExitCode}");
                try
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        try
                        {
                            AppendOutput($"[终端] 进程已退出，退出码: {_process.ExitCode}\n", Brushes.Gray);
                            DisposeProcess(); // 退出后自动清理
                        }
                        catch (Exception ex)
                        {
                            Core.App.CurrentLogger?.Error(ex);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Core.App.CurrentLogger?.Error(ex);
                }
            };

            AppendOutput($"[终端] 已启动 Shell: {_shell}\n", Brushes.Green);
        }
        catch (Exception ex)
        {
            LogDebug($"启动进程失败: {ex.Message}\n{ex.StackTrace}");
            AppendOutput($"[错误] 启动终端失败: {ex.Message}\n", Brushes.Red);
            DisposeProcess();
        }
    }

// 抽离进程配置（重点修复 Linux 启动参数）
    private void ConfigureProcessStartInfo()
    {
        var psi = _process.StartInfo;
        psi.FileName = _shell;
        psi.UseShellExecute = false;
        psi.RedirectStandardInput = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.CreateNoWindow = true;

        // 跨平台参数配置（核心：Linux 禁用交互模式）
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // PowerShell 配置（兼容 & 语法 + 强制 UTF8 + 无缓冲输出）
            if (_shell.Contains("powershell", StringComparison.OrdinalIgnoreCase))
            {
                psi.Arguments = "-NoLogo -NoExit -ExecutionPolicy Bypass";
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;
            }
            // Cmd 备用配置
            else if (_shell.EndsWith("cmd.exe", StringComparison.OrdinalIgnoreCase))
            {
                psi.Arguments = "/K chcp 65001 >nul";
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;
            }

            // 强制禁用输出缓冲（关键：解决自定义程序无输出）
            psi.EnvironmentVariables["PYTHONUNBUFFERED"] = "1"; // 兼容Python程序
            psi.EnvironmentVariables["NODE_NO_READLINE"] = "1"; // 兼容Node程序
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            psi.Arguments = "";
            psi.EnvironmentVariables["LANG"] = "en_US.UTF-8";
            psi.EnvironmentVariables["LC_ALL"] = "en_US.UTF-8";
            psi.EnvironmentVariables["TERM"] = "dumb";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            psi.FileName = "/bin/bash";
            psi.Arguments = "";
            psi.EnvironmentVariables["LANG"] = "en_US.UTF-8";
        }
    }

// 重构 DisposeProcess（增加取消令牌）
    private void DisposeProcess()
    {
        LogDebug("开始释放进程资源");
        try
        {
            // 1. 取消异步读取任务
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            // 2. 清理进程
            if (_process != null)
            {
                bool hasExited;
                try
                {
                    hasExited = _process.HasExited;
                }
                catch (InvalidOperationException)
                {
                    hasExited = true;
                }
                catch (NullReferenceException)
                {
                    hasExited = true;
                }

                if (!hasExited)
                {
                    try
                    {
                        // Linux 下强制杀进程（避免 sh 僵尸进程）
                        _process.Kill();
                        LogDebug("强制终止进程");
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"终止进程失败: {ex.Message}");
                    }
                }

                _process.Dispose();
                LogDebug("进程已释放");
            }

            // 3. 清理输入流
            _inputWriter?.Dispose();
            _inputWriter = null;

            // 4. 重置引用
            _process = null;
            _disposed = true;
        }
        catch (Exception ex)
        {
            LogDebug($"释放资源失败: {ex.Message}");
        }

        LogDebug("进程资源释放完成");
    }

// 重写 GetDefaultShell（Linux 优先 sh）
    public static string GetDefaultShell()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "powershell.exe";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return GetMacOSDefaultShell();
        }
        else
        {
            return "/bin/sh"; // Linux 不用 bash，避免交互阻塞
        }
    }

    // 新增日志辅助方法
    private void LogDebug(string msg)
    {
        System.Console.OutputEncoding = Encoding.UTF8;
        System.Console.WriteLine($"\e[33m[TerminalControl]\e[0m {DateTime.Now:HH:mm:ss} {msg}", Encoding.UTF8);
    }

// 优化 ReadOutputAsync（增加缓冲刷新 + 更稳定的读取）
    private async Task ReadOutputAsync(CancellationToken ct)
    {
        LogDebug("开始读取标准输出（异步模式）");
        if (_process == null || _disposed)
        {
            return;
        }

        var buffer = new byte[4096];
        var encoding = Encoding.UTF8; // Windows 已强制 UTF8
        var decoder = encoding.GetDecoder();
        var charBuffer = new char[4096];

        try
        {
            var stream = _process.StandardOutput.BaseStream;
            while (!ct.IsCancellationRequested && !_disposed)
            {
                // 检查进程是否存活
                bool hasExited;
                try
                {
                    hasExited = _process.HasExited;
                }
                catch (InvalidOperationException)
                {
                    hasExited = true;
                }

                if (hasExited)
                {
                    break;
                }

                // 非阻塞读取（移除1秒超时，改用流的DataAvailable判断）
                var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                LogDebug($"读取到 {bytesRead} 字节输出");
                if (bytesRead == 0)
                {
                    break;
                }

                var charCount = decoder.GetChars(buffer, 0, bytesRead, charBuffer, 0);
                var text = new string(charBuffer, 0, charCount);

                // UI更新（确保线程安全 + 即时刷新）
                Dispatcher.UIThread.Post(() =>
                {
                    OutputBox.Document.Insert(OutputBox.Document.TextLength, text);
                    OutputBox.ScrollToEnd();
                    // 保留错误解析逻辑
                    try
                    {
                        var solution = TerminalHelper.GetSolution(text);
                        if (solution != null)
                        {
                            _ = RYCBApiConverter.GetTunnelErrorInfoAsync(solution.Flag).ContinueWith(t =>
                            {
                                if (t is { IsCompletedSuccessfully: true, Result: not null })
                                {
                                    Dispatcher.UIThread.Post(() =>
                                    {
                                        ErrorIcon.Symbol = Symbol.ReportHacked;
                                        ErrorText.Text = t.Result.data.Info;
                                        SolutionBox.Text = t.Result.data.Solution[0];
                                        _tunnelErrorInfoShell = t.Result;
                                    });
                                }
                            }, ct);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"解析错误信息失败: {ex.Message}");
                    }
                });
            } // 无数据时短暂休眠（减少CPU占用）

            await Task.Delay(50, ct);
        }
        catch (OperationCanceledException)
        {
            LogDebug("输出读取任务被取消");
        }

        catch (IOException ex)
        {
            LogDebug($"输出流异常: {ex.Message}");
        }

        catch (Exception ex)
        {
            LogDebug($"输出读取崩溃: {ex.Message}\n{ex.StackTrace}");
            Dispatcher.UIThread.Post(() => AppendOutput($"[错误] 读取输出失败: {ex.Message}\n", Brushes.Red));
        }
    }

// 优化 ReadErrorAsync（和输出逻辑保持一致）
    private async Task ReadErrorAsync(CancellationToken ct)
    {
        LogDebug("开始读取标准错误（异步模式）");
        if (_process == null || _disposed)
        {
            return;
        }

        var buffer = new byte[4096];
        var encoding = Encoding.UTF8;
        var decoder = encoding.GetDecoder();
        var charBuffer = new char[4096];

        try
        {
            var stream = _process.StandardError.BaseStream;
            while (!ct.IsCancellationRequested && !_disposed)
            {
                bool hasExited;
                try
                {
                    hasExited = _process.HasExited;
                }
                catch (InvalidOperationException)
                {
                    hasExited = true;
                }

                if (hasExited)
                {
                    break;
                }

                var bytesRead = await stream.ReadAsync(buffer, ct);
                if (bytesRead == 0)
                {
                    break;
                }

                var charCount = decoder.GetChars(buffer, 0, bytesRead, charBuffer, 0);
                var errorText = new string(charBuffer, 0, charCount);

                Dispatcher.UIThread.Post(() =>
                {
                    OutputBox.Document.Insert(OutputBox.Document.TextLength, errorText);
                    OutputBox.ScrollToEnd();
                });
                await Task.Delay(50, ct);
            }
        }
        catch (OperationCanceledException)
        {
            LogDebug("错误读取任务被取消");
        }
        catch (Exception ex)
        {
            LogDebug($"错误读取崩溃: {ex.Message}\n{ex.StackTrace}");
            Dispatcher.UIThread.Post(() => AppendOutput($"[错误] 读取错误输出失败: {ex.Message}\n", Brushes.Red));
        }
    }

    private async Task ReadErrorOutput()
    {
        // 增加进程空值检查 + 退出标记
        if (_process == null || _disposed)
        {
            return;
        }

        try
        {
            var buffer = new byte[4096];
            var currentEncoding = GetConsoleEncoding();
            var decoder = currentEncoding.GetDecoder();
            var charBuffer = new char[4096];

            var stream = _process?.StandardError?.BaseStream;
            if (stream == null)
            {
                return;
            }

            while (!_disposed && !_process?.HasExited == true) // 增加 HasExited 前置检查
            {
                try
                {
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                    if (bytesRead > 0)
                    {
                        var detectedEncoding = DetectOutputEncoding(buffer, bytesRead);
                        if (detectedEncoding != currentEncoding)
                        {
                            currentEncoding = detectedEncoding;
                            decoder = currentEncoding.GetDecoder();
                        }

                        var charCount = decoder.GetChars(buffer, 0, bytesRead, charBuffer, 0);
                        var errorText = new string(charBuffer, 0, charCount);

                        Dispatcher.UIThread.Post(() =>
                        {
                            OutputBox.Document.Insert(OutputBox.Document.TextLength, errorText);
                            OutputBox.ScrollToEnd();
                        });
                    }
                    else
                    {
                        // 无数据时短暂休眠，避免空循环
                        await Task.Delay(10).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    break; // 任务取消时退出
                }
                catch (Exception ex)
                {
                    if (!_disposed)
                    {
                        Dispatcher.UIThread.Post(() =>
                            AppendOutput($"\e[33m错误输出读取失败: {ex.Message}\n\e[0m", Brushes.Red));
                    }

                    break; // 异常时直接退出循环
                }
            }
        }
        catch (InvalidOperationException ex)
        {
            // 捕获进程未关联的异常，避免崩溃
            if (!_disposed)
            {
                Dispatcher.UIThread.Post(() =>
                    AppendOutput($"\e[33m进程状态异常: {ex.Message}\n\e[0m", Brushes.Red));
            }
        }
        catch (Exception ex)
        {
            if (!_disposed)
            {
                Dispatcher.UIThread.Post(() =>
                    AppendOutput($"\e[33m错误输出读取失败: {ex.Message}\n\e[0m", Brushes.Red));
            }
        }
    }

    private void SetupWindowsTerminal()
    {
        _process.StartInfo.FileName = _shell;
        _process.StartInfo.UseShellExecute = false; // 必须为false才能重定向
        _process.StartInfo.RedirectStandardInput = true;
        _process.StartInfo.RedirectStandardOutput = true;
        _process.StartInfo.RedirectStandardError = true;
        _process.StartInfo.CreateNoWindow = true; // 这里为true，所以不能附加控制台
        _process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
        _process.StartInfo.StandardErrorEncoding = Encoding.UTF8;

        // 对于PowerShell
        if (_shell.Contains("powershell", StringComparison.OrdinalIgnoreCase))
        {
            _process.StartInfo.Arguments = "-NoLogo -NoExit";
        }
        // 对于cmd.exe
        else if (_shell.EndsWith("cmd.exe", StringComparison.OrdinalIgnoreCase))
        {
            _process.StartInfo.Arguments = "/K";
        }

        // 启用事件并在退出时输出更多诊断信息
        _process.EnableRaisingEvents = true;
        _process.Exited += (s, e) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var err = "";
                    try
                    {
                        err = _process.StandardError?.ReadToEnd() ?? "";
                    }
                    catch
                    {
                        /* ignore */
                    }

                    if (!string.IsNullOrEmpty(err))
                    {
                        AppendOutput(err + "\r\n", Brushes.Red);
                    }

                    AppendOutput($"\r\n进程已退出，退出代码: {_process.ExitCode}\r\n", Brushes.Gray);
                }
                catch (InvalidOperationException)
                {
                }
            });
        };
    }

    private void SetupMacOSTerminal()
    {
        // Use /usr/bin/script on macOS to allocate a pty. Note: BSD script (macOS) does NOT support -c,
        // instead the command and arguments follow the filename argument directly:
        //   /usr/bin/script -q /dev/null /bin/zsh -il
        const string scriptPath = "/usr/bin/script";

        if (File.Exists(scriptPath))
        {
            _process.StartInfo.FileName = scriptPath;

            // macOS script syntax: script [-q] [file] [command ...]
            // We pass /dev/null as the "typescript" file and then the shell + args.
            // Example: /usr/bin/script -q /dev/null /bin/zsh -il
            _process.StartInfo.Arguments = $"-q /dev/null {_shell} -il";
        }
        else
        {
            // Fallback: run the shell directly as interactive login shell.
            // Some shells still may behave differently without a pty, but this is a best-effort fallback.
            _process.StartInfo.FileName = _shell;
            _process.StartInfo.Arguments = "-il";
        }

        _process.StartInfo.UseShellExecute = false;
        _process.StartInfo.RedirectStandardInput = true;
        _process.StartInfo.RedirectStandardOutput = true;
        _process.StartInfo.RedirectStandardError = true;
        _process.StartInfo.CreateNoWindow = true;

        // Ensure environment for interactive shells
        _process.StartInfo.EnvironmentVariables["TERM"] = "xterm-256color";
        _process.StartInfo.EnvironmentVariables["COLORTERM"] = "truecolor";
        _process.StartInfo.EnvironmentVariables["LANG"] = "en_US.UTF-8";
        _process.StartInfo.EnvironmentVariables["LC_ALL"] = "en_US.UTF-8";

        _process.EnableRaisingEvents = true;
        _process.Exited += (s, e) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var err = "";
                    try
                    {
                        err = _process.StandardError?.ReadToEnd() ?? "";
                    }
                    catch
                    {
                        /* ignore */
                    }

                    if (!string.IsNullOrEmpty(err))
                    {
                        AppendOutput(err + "\r\n", Brushes.Red);
                    }

                    AppendOutput($"\r\n进程已退出，退出代码: {_process.ExitCode}\r\n", Brushes.Gray);
                }
                catch (InvalidOperationException)
                {
                }
            });
        };
    }

    private void SetupLinuxTerminal()
    {
        _process.StartInfo.FileName = _shell;
        _process.StartInfo.UseShellExecute = false;
        _process.StartInfo.RedirectStandardInput = true;
        _process.StartInfo.RedirectStandardOutput = true;
        _process.StartInfo.RedirectStandardError = true;
        _process.StartInfo.CreateNoWindow = true;

        // 修复环境变量，避免编码/终端类型错误
        _process.StartInfo.EnvironmentVariables["TERM"] = "xterm-256color";
        _process.StartInfo.EnvironmentVariables["COLORTERM"] = "truecolor";
        _process.StartInfo.EnvironmentVariables["LANG"] = "en_US.UTF-8";
        _process.StartInfo.EnvironmentVariables["LC_ALL"] = "en_US.UTF-8";

        // 优化 bash 参数，避免交互模式卡死
        if (_shell.EndsWith("bash", StringComparison.OrdinalIgnoreCase))
        {
            _process.StartInfo.Arguments = "--noprofile --norc -i -l"; // 增加登录模式，避免环境变量缺失
        }
        else if (_shell.EndsWith("zsh", StringComparison.OrdinalIgnoreCase))
        {
            _process.StartInfo.Arguments = "-i -l"; // zsh 兼容参数
        }

        _process.EnableRaisingEvents = true;
        _process.Exited += (s, e) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var err = "";
                    try
                    {
                        err = _process.StandardError?.ReadToEnd() ?? "";
                    }
                    catch
                    {
                        /* ignore */
                    }

                    if (!string.IsNullOrEmpty(err))
                    {
                        AppendOutput(err + "\r\n", Brushes.Red);
                    }

                    AppendOutput($"\r\n进程已退出，退出代码: {_process.ExitCode}\r\n", Brushes.Gray);
                }
                catch (InvalidOperationException)
                {
                }
            });
        };
    }

    private Encoding DetectOutputEncoding(byte[] buffer, int bytesRead)
    {
        // 检查UTF-8 BOM
        if (bytesRead >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
        {
            return Encoding.UTF8;
        }

        // 更精确的编码检测
        var utf8Score = 0;
        var gb2312Score = 0;
        var validAscii = 0;

        for (var i = 0; i < bytesRead; i++)
        {
            // ASCII字符 (0-127)
            if (buffer[i] <= 0x7F)
            {
                validAscii++;
                continue;
            }

            // UTF-8多字节序列检测
            if (i < bytesRead - 1 && (buffer[i] & 0xE0) == 0xC0 && (buffer[i + 1] & 0xC0) == 0x80)
            {
                utf8Score += 2;
                i++; // 跳过下一个字节
            }
            else if (i < bytesRead - 2 && (buffer[i] & 0xF0) == 0xE0 &&
                     (buffer[i + 1] & 0xC0) == 0x80 && (buffer[i + 2] & 0xC0) == 0x80)
            {
                utf8Score += 3;
                i += 2; // 跳过两个字节
            }

            // GB2312中文范围检测
            if (i < bytesRead - 1 && buffer[i] >= 0xA1 && buffer[i] <= 0xF7 &&
                buffer[i + 1] >= 0xA1 && buffer[i + 1] <= 0xFE)
            {
                gb2312Score += 2;
                i++; // 跳过下一个字节
            }
        }

        // 如果大部分是ASCII，优先使用UTF-8
        if (validAscii > bytesRead * 0.8)
        {
            return Encoding.UTF8;
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // 根据得分选择编码
        if (utf8Score > gb2312Score * 1.5)
        {
            return Encoding.UTF8;
        }

        if (gb2312Score > utf8Score * 1.5)
        {
            try
            {
                return Encoding.GetEncoding("GB2312");
            }
            catch
            {
                return Encoding.GetEncoding("GBK");
            }
        }

        // 默认情况
        return Encoding.UTF8;
    }

    private void ReadOutputLoop()
    {
        if (_process == null || _disposed)
        {
            return; // 前置检查
        }

        var buffer = new byte[4096];
        var currentEncoding = GetConsoleEncoding();
        var decoder = currentEncoding.GetDecoder();
        var charBuffer = new char[4096];

        var stream = _process?.StandardOutput?.BaseStream;
        if (stream == null)
        {
            return;
        }

        while (!_disposed)
        {
            try
            {
                // 检查进程状态，退出循环
                if (_process is { HasExited: true })
                {
                    break;
                }

                var bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    var detectedEncoding = DetectOutputEncoding(buffer, bytesRead);
                    if (detectedEncoding != currentEncoding)
                    {
                        currentEncoding = detectedEncoding;
                        decoder = currentEncoding.GetDecoder();
                    }

                    var charCount = decoder.GetChars(buffer, 0, bytesRead, charBuffer, 0);
                    var text = new string(charBuffer, 0, charCount);

                    Dispatcher.UIThread.Post(async void () =>
                    {
                        try
                        {
                            OutputBox.Document.Insert(OutputBox.Document.TextLength, text);
                            var solution = TerminalHelper.GetSolution(text);
                            if (solution is not null)
                            {
                                var onlineSolution = await RYCBApiConverter.GetTunnelErrorInfoAsync(solution.Flag);
                                ErrorIcon.Symbol = Symbol.ReportHacked;
                                ErrorText.Text = onlineSolution?.data.Info;
                                SolutionBox.Text = onlineSolution?.data.Solution[0];
                                _tunnelErrorInfoShell = onlineSolution!;
                            }

                            OutputBox.ScrollToEnd();
                        }
                        catch (Exception e)
                        {
                            Core.App.CurrentLogger?.Error(e);
                        }
                    });
                }
                else
                {
                    if (_process?.HasExited == true)
                    {
                        break;
                    }

                    Thread.Sleep(10);
                }
            }
            catch (IOException)
            {
                // 流关闭时退出循环（Linux下常见）
                break;
            }
            catch (Exception ex)
            {
                if (!_disposed)
                {
                    Dispatcher.UIThread.Post(() =>
                        AppendOutput($"\e[31m终端错误: {ex.Message}\n\e[00m", Brushes.Red));
                }

                break; // 异常时退出线程，避免卡死
            }
        }
    }

// 获取控制台编码
    private Encoding GetConsoleEncoding()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                // Windows下使用GB2312编码
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                return Encoding.GetEncoding("GB2312");
            }
            catch
            {
                return Encoding.Default;
            }
        }

        // macOS/Linux使用UTF-8
        return Encoding.UTF8;
    }

    public async Task SendCommandAsync(string command)
    {
        if (string.IsNullOrEmpty(command))
        {
            return;
        }

        _history.Add(command);

        if (command.Equals("exit", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (IsClearCommand(command))
        {
            ClearOutput();
            return;
        }

        try
        {
            await _inputWriter.WriteLineAsync(command).ConfigureAwait(false);
            await _inputWriter.FlushAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppendOutput($"\e[31mFailed to send command: {ex.Message}\r\n\e[00m", Brushes.Red);
        }
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        _ctrlPressed = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (e.Key == Key.Enter)
        {
            if (string.IsNullOrEmpty(InputBox.Text))
            {
                return;
            }

            _history.Add(InputBox.Text);
            _historyIndex = _history.Count;

            if (IsClearCommand(InputBox.Text))
            {
                ClearOutput();
                InputBox.Text = string.Empty;
                return;
            }

            try
            {
                AppendOutput($"{GetPrompt()}{InputBox.Text}\r\n", Brushes.Cyan);
                _inputWriter.WriteLine(InputBox.Text);
                _inputWriter.Flush();
            }
            catch (Exception ex)
            {
                AppendOutput($"\e[31mFailed to send command: {ex.Message}\r\n\e[00m", Brushes.Red);
                StartTerminal(_shell); // Try to restart
            }

            InputBox.Text = string.Empty;
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            if (_historyIndex > 0)
            {
                _historyIndex--;
                InputBox.Text = _history[_historyIndex];
            }

            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            if (_historyIndex < _history.Count - 1)
            {
                _historyIndex++;
                InputBox.Text = _history[_historyIndex];
            }
            else
            {
                _historyIndex = _history.Count;
                InputBox.Text = string.Empty;
            }

            e.Handled = true;
        }
        else if (e.Key == Key.C && _ctrlPressed)
        {
            SendCtrlCCommand();
            e.Handled = true;
        }
        else if (e.Key == Key.L && _ctrlPressed)
        {
            ClearOutput();
            e.Handled = true;
        }
    }

    public void SendCtrlCCommand()
    {
        try
        {
            if (_process == null || _process.HasExited)
            {
                AppendOutput("\e[33m没有活动的进程可以中断\r\n\e[00m", Brushes.Yellow);
                return;
            }

            AppendOutput("\e[33m^C\r\n\e[00m", Brushes.Yellow);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // 使用taskkill终止进程树（最可靠的方法）
                using var killProcess = new Process();
                killProcess.StartInfo.FileName = "taskkill";
                killProcess.StartInfo.Arguments = $"/PID {_process.Id} /T /F";
                killProcess.StartInfo.UseShellExecute = false;
                killProcess.StartInfo.CreateNoWindow = true;
                killProcess.Start();

                // 等待一段时间让进程终止
                if (killProcess.WaitForExit(2000))
                {
                    if (killProcess.ExitCode == 0)
                    {
                        AppendOutput("\e[32m进程已终止\r\n\e[00m", Brushes.Green);
                    }
                    else
                    {
                        AppendOutput($"\e[31m终止进程失败，退出代码: {killProcess.ExitCode}\r\n\e[00m", Brushes.Red);

                        // 尝试直接杀死进程
                        try
                        {
                            if (!_process.HasExited)
                            {
                                _process.Kill();
                                AppendOutput("\e[33m进程已被强制终止\r\n\e[00m", Brushes.Orange);
                            }
                        }
                        catch (Exception killEx)
                        {
                            AppendOutput($"\e[31m强制终止失败: {killEx.Message}\r\n\e[00m", Brushes.Red);
                        }
                    }
                }
                else
                {
                    AppendOutput("\e[31m终止进程超时\r\n\e[00m", Brushes.Red);
                    killProcess.Kill();
                }
            }
            else
            {
                // Linux/macOS: 先尝试优雅终止，再强制终止
                try
                {
                    Process.Start("kill", $"-15 {_process.Id}")?.WaitForExit(1000);

                    // 给进程一些时间响应
                    Thread.Sleep(500);

                    if (!_process.HasExited)
                    {
                        Process.Start("kill", $"-9 {_process.Id}")?.WaitForExit(1000);
                        Thread.Sleep(300);

                        if (!_process.HasExited)
                        {
                            _process.Kill();
                            AppendOutput("\e[33m进程已被强制终止\r\n\e[00m", Brushes.Orange);
                        }
                        else
                        {
                            AppendOutput("\e[32m进程已终止\r\n\e[00m", Brushes.Green);
                        }
                    }
                    else
                    {
                        AppendOutput("\e[32m进程已终止\r\n\e[00m", Brushes.Green);
                    }
                }
                catch (Exception ex)
                {
                    AppendOutput($"终止进程失败: {ex.Message}\r\n", Brushes.Red);
                }
            }
        }
        catch (Exception ex)
        {
            AppendOutput($"发送中断信号失败: {ex.Message}\r\n", Brushes.Red);
        }
    }

    private void InputBox_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.LeftCtrl or Key.RightCtrl)
        {
            _ctrlPressed = false;
        }
    }

    private string GetPrompt()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return _shell.Contains("powershell", StringComparison.OrdinalIgnoreCase)
                ? "PS> "
                : "> ";
        }

        return "$ ";
    }

    private static bool IsClearCommand(string command)
    {
        return command.Trim().Equals("clear", StringComparison.OrdinalIgnoreCase) ||
               command.Trim().Equals("cls", StringComparison.OrdinalIgnoreCase);
    }

    private void AppendOutput(string text, ISolidColorBrush color)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // 直接插入文本，颜色转换器会处理颜色
            OutputBox?.Document.Insert(OutputBox.Document.TextLength, text);
            OutputBox?.ScrollToEnd();
        });
    }

    private static string RemoveAnsiCodes(string text)
    {
        return Regex.Replace(text, @"\x1B\[([0-9;]*)m", string.Empty);
    }

// 清除输出
    private void ClearOutput()
    {
        Dispatcher.UIThread.Post(() =>
        {
            OutputBox.Document.Text = string.Empty;
        });
    }

    [Obsolete]
//TODO
    private void DisposeProcessBak()
    {
        try
        {
            if (_process != null)
            {
                // 先检查进程是否还在运行
                bool hasExited;
                try
                {
                    hasExited = _process.HasExited;
                }
                catch (InvalidOperationException)
                {
                    hasExited = true; // 进程未关联时视为已退出
                }

                if (!hasExited)
                {
                    // 尝试优雅终止
                    try
                    {
                        _inputWriter?.WriteLine("exit");
                        _inputWriter?.Flush();
                        if (!_process.WaitForExit(500))
                        {
                            _process.Kill();
                        }
                    }
                    catch
                    {
                        _process.Kill();
                    }
                }

                _process.Dispose();
            }

            _inputWriter?.Dispose();

            // 终止读取线程
            if (_outputReaderThread is { IsAlive: true })
            {
                _outputReaderThread.Join(500);
            }

            _outputReaderThread = null; // 重置线程引用

            _process = null; // 重置进程引用
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error disposing process: {ex.Message}");
            Core.App.CurrentLogger?.Error(ex);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        DisposeProcess();
    }

    [GeneratedRegex(@"\x1B\[([0-9;]*)m")]
    private static partial Regex AnsiColors();

    private async void ViewErrorDetails(object? sender, RoutedEventArgs e)
    {
        try
        {
            var cd = new TaskDialog
            {
                Title = "错误详情",
                SubHeader = $"{_tunnelErrorInfoShell.data.Flag}: {_tunnelErrorInfoShell.data.Info}",
                Content = new TunnelErrorPresenter(_tunnelErrorInfoShell.data.Solution),
                Buttons =
                {
                    TaskDialogButton.OKButton
                },
                IconSource = new SymbolIconSource { Symbol = Symbol.Admin },
                XamlRoot = Core.App.MainWindow
            };
            await cd.ShowAsync();
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Error(ex);
        }
    }
}