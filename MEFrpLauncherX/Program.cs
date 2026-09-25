#define AOT
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using MEFrpLauncherX.Core;
using ReactiveUI.Avalonia;
using SecretLib;
using Sentry;
using static MEFrpLauncherX.Core.StringUtils;

namespace MEFrpLauncherX;

internal partial class Program
{
    private const string AppPipeName = "tech.rycb.pml2";
    private static Mutex? _mutex;
    private static CancellationTokenSource? _pipeServerCts;

    public static Process SplashProcess
    {
        get;
        private set;
    }

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        // 记录启动时间，供崩溃报告计算真实运行时长
        CrashHandler.StartupTime = DateTime.Now;
        //StartupTransaction = SentrySdk.StartTransaction("app.startup", "app.lifecycle");
        // 1. 定义一个全局唯一的Mutex名称（推荐使用反向域名格式）

        // 2. 尝试创建或打开已存在的命名Mutex
        // 将 mutex 存入 static 字段，不依赖 using var 来维持生命周期
        _mutex = new Mutex(true, $"Global\\{AppPipeName}", out var createdNew);
        if (!createdNew)
        {
            // 已有实例在运行，尝试激活它
            ActivateExistingInstance();
            Environment.Exit(0); // 退出当前进程
            return 0; // 退出当前进程
        }

        // 启动 Named Pipe 服务器，监听来自第二个实例的"显示窗口"请求
        StartPipeServer();
        // 26.3.1 M1：Splash 进度管道名（与单实例激活管道 tech.rycb.pml2 严格分离）
        var splashPipeName = $"tech.rycb.pml2.splash.{Environment.ProcessId}";
        var splashFile = GetPlatformExe(Path.Combine(Core.App.StartupPath, "Tools", "splash"), true);
        // if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        // {
        //     File.SetUnixFileMode(splashFile, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        // }

        System.Console.OutputEncoding = Encoding.UTF8;
        // AssemblyLoadContext.Default.Resolving += (ctx, assemblyName) =>
        // {
        //     var assemblyPath = Path.Combine(AppContext.BaseDirectory, "assemblies", $"{assemblyName.Name}.dll");
        //     return File.Exists(assemblyPath) ? ctx.LoadFromAssemblyPath(assemblyPath) : null;
        // };

        // 26.3.1 M2：早读 Splash 配置（Splash 在 Avalonia/ConfigManager 就绪前启动，直接解析 Settings.json）
        var splashConfig = ReadSplashConfig();
        try
        {
            if (!splashConfig.Enabled)
            {
                System.Console.WriteLine("[INFO] Splash is disabled by user settings.");
                Core.App.CurrentLogger?.Log("启动画面已由用户设置关闭。", EnumLogType.Debug);
            }
            else if (!File.Exists(splashFile))
            {
                System.Console.WriteLine("\e[33m[WARNING] The Splash file is missing. May need to reinstall.\e[0m");
                Core.App.CurrentLogger?.Log("启动画面文件缺失。", EnumLogType.Warn);
            }
            else
            {
                var p = Process.Start(new ProcessStartInfo
                {
                    FileName = splashFile,
                    Arguments =
                        $"-v \"{Core.App.Version} ‘{App.Codename}’ \" -b \"{ResolveSplashImage(splashConfig)}\" --style \"{splashConfig.Style}\" --pipe \"{splashPipeName}\""
                });
                SplashProcess = p;
                // 26.3.1 M1：主程序启动阶段进度 → Splash 管道（App.SplashService 由本服务实现）
                App.SplashService = new Services.PipeSplashService(splashPipeName);
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"\e[31m[ERROR] Failed to start splash process: {ex.Message}\e[0m");
            Core.App.CurrentLogger?.Log($"启动画面进程启动失败: {ex.Message}", EnumLogType.Error);
        }
#if !DEBUG
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += ProcessUnhandledExceptions;
        try
        {
#endif
        if (args.Length > 0)
        {
            if (!ProcessStartupArguments(args[0]))
            {
                SplashProcess?.Kill();
                var isUnpack = !args.FirstOrDefault(x => x.Equals("unpack")).IsNullOrEmpty();
                var isPack = !args.FirstOrDefault(x => x.Equals("pack")).IsNullOrEmpty();
                if (isUnpack)
                {
                    var pmlaFile = "";
                    var outPath = "";
                    try
                    {
                        pmlaFile = args.FirstOrDefault(x => x.ReplaceAnyToOne(["--", "/p:", "/"]).StartsWith("pmlaFile=") || x.ReplaceAnyToOne(["--", "/p:", "/"]).StartsWith("pmla="))
                            ?.Split('=')[1];
                        outPath = args.FirstOrDefault(x => x.ReplaceAnyToOne(["--", "/p:", "/"]).StartsWith("outPath=") || x.ReplaceAnyToOne(["--", "/p:", "/"]).StartsWith("output="))
                            ?.Split('=')[1];
                    }
                    catch (IndexOutOfRangeException)
                    {
                        System.Console.Error.WriteLine("Invalid pmlaFile argument.");
                        Environment.Exit(2);
                        return 2;
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine("Unknown Error: {0}", ex);
                    }

                    if (pmlaFile.IsNullOrEmpty() || outPath.IsNullOrEmpty())
                        return 2;
                    var banner = new string('=', 30);
                    System.Console.WriteLine(banner);
                    System.Console.WriteLine("PMLA Unpack Mode");
                    System.Console.WriteLine($"Unpacking {pmlaFile}(New Format: {PMLAXHelper.IsPmlaxFile(pmlaFile)}) to {outPath}");
                    System.Console.WriteLine(banner);
                    PMLAHelper.UnpackPmla(pmlaFile, outPath, (progress, status) =>
                    {
                        System.Console.WriteLine($"[{progress}] {status}");
                    });
                    System.Console.WriteLine("Unpack completed.");
                    return 0;
                }
                if (isPack)
                {
                    var inPath = "";
                    var outPmla = "";
                    var isNewFormat = true;
                    try
                    {
                        inPath = args.FirstOrDefault(x => x.ReplaceAnyToOne(["--", "/p:", "/"]).StartsWith("inPath=") || x.ReplaceAnyToOne(["--", "/p:", "/"]).StartsWith("input="))
                            ?.Split('=')[1];
                        outPmla = args.FirstOrDefault(x => x.ReplaceAnyToOne(["--", "/p:", "/"]).StartsWith("outPmla=") || x.ReplaceAnyToOne(["--", "/p:", "/"]).StartsWith("pmla="))
                            ?.Split('=')[1];
                        isNewFormat = args.FirstOrDefault(x => x.ReplaceAnyToOne(["--", "/p:", "/"]).StartsWith("useNewFormat=") || x.ReplaceAnyToOne(["--", "/p:", "/"]).StartsWith("newFormat="))
                            ?.Split('=')[1] is null or "true";
                    }
                    catch (IndexOutOfRangeException)
                    {
                        System.Console.Error.WriteLine("Invalid pmlaFile argument.");
                        Environment.Exit(2);
                        return 2;
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine("Unknown Error: {0}", ex);
                    }

                    if (inPath.IsNullOrEmpty() || outPmla.IsNullOrEmpty())
                    {
                        Environment.Exit(2);
                        return 2;
                    }
                    var banner = new string('=', 30);
                    System.Console.WriteLine(banner);
                    System.Console.WriteLine("PMLA Pack Mode");
                    System.Console.WriteLine($"Packing {inPath}(New Format: {isNewFormat}) to {outPmla}");
                    System.Console.WriteLine(banner);
                    if (isNewFormat)
                        PMLAXHelper.PackDirectory(inPath, outPmla, (progress, status) =>
                        {
                            System.Console.WriteLine($"[{progress}] {status}");
                        });
                    else
                        PMLAHelper.PackDirectory(inPath, outPmla, (progress, status) =>
                        {
                            System.Console.WriteLine($"[{progress}] {status}");
                        });
                    System.Console.WriteLine("Pack completed.");
                    Environment.Exit(0);
                    return 0;
                }
                System.Console.Error.WriteLine("[E] Invalid Arguments.");
                Environment.Exit(1);
                return 1;
            }
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
#if !DEBUG
        }
        catch (Exception ex)
        {
            if (ex.GetType().Name == "QuicException")
            {
            }
            else
            {
                HandleException(ex);
            }
            return 1;
        }
        finally
        {
            _pipeServerCts?.Cancel();
            _pipeServerCts?.Dispose();
            _mutex?.ReleaseMutex();
            _mutex?.Close();
        }
#endif
        return 0;
    }


    /// <summary>
    ///     启动 Named Pipe 服务器，在后台线程上监听第二个实例的"激活窗口"请求
    /// </summary>
    private static void StartPipeServer()
    {
        _pipeServerCts = new CancellationTokenSource();
        var token = _pipeServerCts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(
                        AppPipeName,
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    // 等待客户端连接（第二个实例）
                    await server.WaitForConnectionAsync(token);

                    // 读取激活信号
                    using var reader = new StreamReader(server, Encoding.UTF8);
                    var signal = await reader.ReadLineAsync(token);

                    if (signal == "SHOW")
                    {
                        // 在 UI 线程上显示主窗口
                        Dispatcher.UIThread.Post(() =>
                        {
                            if (Core.App.MainWindow is not null)
                            {
                                Core.App.MainWindow.Show();
                                Core.App.MainWindow.Activate();
                                Core.App.MainWindow.WindowState = Avalonia.Controls.WindowState.Normal;
                            }
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException)
                {
                    // 客户端断开连接，继续监听下一个
                }
                catch (Exception ex)
                {
                    Core.App.CurrentLogger?.Log(
                        $"Named Pipe 服务器异常: {ex.Message}",
                        EnumLogType.Debug);
                }
            }
        }, token);
    }

    internal static void ActivateExistingInstance()
    {
        try
        {
            // 首选：通过 Named Pipe 通知第一个实例显示窗口
            using var client = new NamedPipeClientStream(".", AppPipeName, PipeDirection.Out);
            // 给第一个实例一点时间响应，最多等 2 秒
            client.Connect(2000);

            using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
            writer.WriteLine("SHOW");
            return; // 成功发送信号，直接返回
        }
        catch (TimeoutException)
        {
            // Named Pipe 超时，可能是旧版本第一个实例不支持 Pipe，回退到 Win32 API
        }
        catch (IOException)
        {
            // Pipe 连接失败
        }

        // 回退方案：Win32 API 方式（兼容旧版本或 Pipe 不可用时）
        ActivateExistingInstanceViaWin32();
    }

    /// <summary>
    ///     回退方案：通过 Win32 API 激活已存在的实例窗口
    /// </summary>
    private static void ActivateExistingInstanceViaWin32()
    {
        var currentProcess = Process.GetCurrentProcess();
        var processes = Process.GetProcessesByName(currentProcess.ProcessName);

        foreach (var process in processes)
        {
            if (process.Id == currentProcess.Id) continue;

            // 获取主窗口句柄
            var hWnd = process.MainWindowHandle;
            if (hWnd == IntPtr.Zero) continue;

            // Windows平台：直接调用Win32 API激活窗口
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                SetForegroundWindow(hWnd);
                ShowWindow(hWnd, ShowWindowCommands.Restore);
            }
            // macOS/Linux平台：使用系统命令激活窗口
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("osascript", $"-e 'tell application \"{process.ProcessName}\" to activate'");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                try
                {
                    Process.Start("bash", $"-c \"wmctrl -a '{process.ProcessName}' || true\"");
                }
                catch
                {
                }
            }

            break;
        }
    }


    // Windows API 导入（仅Windows平台需要）
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr hWnd, ShowWindowCommands nCmdShow);

    private enum ShowWindowCommands
    {
        Restore = 9
    }

    private static string GetBackground()
    {
        var possiblePaths = new List<string>
        {
            Path.Combine(Core.App.StartupPath, "Resources", "splash.png"),
            Path.Combine(Core.App.StartupPath, "Resources", "splash.jpg"),
            Path.Combine(Core.App.StartupPath, "Resources", "splash.gif"),
            Path.Combine(Core.App.StartupPath, "Resources", "splash.webp"),
            Path.Combine(Core.App.StartupPath, "Resources", "splash.jpeg"),
            Path.Combine(Core.App.StartupPath, "Resources", "splash.bmp"),
            Path.Combine(Core.App.StartupPath, "Resources", "Splash.jpg"),
            Path.Combine(Core.App.StartupPath, "Resources", "Splash.png"),
            Path.Combine(Core.App.StartupPath, "Resources", "Splash.gif"),
            Path.Combine(Core.App.StartupPath, "Resources", "Splash.webp"),
            Path.Combine(Core.App.StartupPath, "Resources", "Splash.jpeg"),
            Path.Combine(Core.App.StartupPath, "Resources", "Splash.bmp")
        };
        foreach (var possiblePath in possiblePaths.Where(File.Exists))
        {
            return possiblePath;
        }

        return possiblePaths[0];
    }

    /// <summary>早读 Splash 配置：直接解析 Config/Settings.json（26.3.1 M2，Avalonia 就绪前可用）</summary>
    private static (bool Enabled, string Style, string CustomImagePath) ReadSplashConfig()
    {
        try
        {
            var configPath = Path.Combine(Core.App.StartupPath, "Config", "Settings.json");
            if (!File.Exists(configPath)) return (true, "default", string.Empty);
            using var doc = JsonDocument.Parse(File.ReadAllText(configPath),
                new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });
            var root = doc.RootElement;
            var hasEnabled = root.TryGetProperty("SplashEnabled", out var enabledProp);
            var enabled = !hasEnabled || enabledProp.ValueKind == JsonValueKind.True;
            var style = root.TryGetProperty("SplashStyle", out var styleProp)
                ? styleProp.GetString() ?? "default"
                : "default";
            var custom = root.TryGetProperty("SplashCustomImagePath", out var customProp)
                ? customProp.GetString() ?? string.Empty
                : string.Empty;
            return (enabled, style, custom);
        }
        catch
        {
            // 解析失败按默认值处理，不影响启动
            return (true, "default", string.Empty);
        }
    }

    /// <summary>按 Splash 配置解析背景图：自定义图片 → 内置样式预设 → 默认图（26.3.1 M2）</summary>
    private static string ResolveSplashImage((bool Enabled, string Style, string CustomImagePath) cfg)
    {
        // 1. 自定义图片优先（校验存在性与扩展名）
        if (!string.IsNullOrEmpty(cfg.CustomImagePath))
        {
            var custom = Path.GetFullPath(cfg.CustomImagePath);
            if (File.Exists(custom) && IsImageFile(custom)) return custom;
        }

        // 3. 回退：现有默认图逻辑（找不到时由 Splash 进程自行降级）
        return GetBackground();
    }

    private static bool IsImageFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp";
    }

    public static bool ProcessStartupArguments(string arg)
    {
        var data = new StartupData
        {
            StartProxyId = -1,
            StartProxyName = string.Empty
        };
        if (!arg.StartsWith("mefrp://"))
        {
            if (arg == "pmla")
            {
                return false;
            }
        }
        else
        {
            var url = arg.Replace("mefrp://", "");
            var args = url.Split('/');
            if (args is ["StartProxy", var idAndOther, ..])
            {
                var res = idAndOther.Split('?');
                var id = res[0];
                if (res[1].StartsWith("Name=", StringComparison.OrdinalIgnoreCase))
                {
                    data.StartProxyName = HttpUtility.UrlDecode(res[1].Replace("Name=", ""));
                }

                data.StartProxyId = int.Parse(id);
            }

            Directory.CreateDirectory(Path.Combine(Core.App.StartupPath, "Cache"));
            File.WriteAllText(Path.Combine(Core.App.StartupPath, "Cache", "startup.json"),
                JsonSerializer.Serialize(data, App.AppJsonSerializerContext.StartupData));
        }

        return true;
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        // 渲染设置独立于 ConfigManager (后者在 Avalonia 初始化后才可用), 从 Config/Render.json 直接读取
        var renderSettings = RenderConfigManager.Load();

        FontManagerOptions options = new();
        if (OperatingSystem.IsLinux())
        {
            // 无字体服务器上系统字体名（如 "Noto Sans CJK SC"）解析不到，
            // 会在创建窗口时抛 "Could not create glyphTypeface. Font family: $Default" 崩溃。
            // 默认字体直接指向内嵌的 HarmonyOS Sans SC，完全不依赖系统 fontconfig。
            options.DefaultFamilyName = "avares://MEFrpLauncherX.Fonts/Fonts/#HarmonyOS Sans SC";
        }

        options.FontFallbacks =
        [
            new FontFallback
            {
                FontFamily = new FontFamily(new Uri("avares://MEFrpLauncherX.Fonts/Fonts/#HarmonyOS Sans SC"),
                    "Harmony OS Sans SC")
            }
        ];

        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .With(options)
            .With(new SkiaOptions
            {
                MaxGpuResourceSizeBytes = (long)NormalizeGpuMemory(renderSettings.GpuMemoryLimitMb) * 1024 * 1024
            })
            // .With(new CompositionOptions
            // {
            //     UseRegionDirtyRectClipping = true,
            //     UseSaveLayerRootClip = true
            // })
            .With(new MacOSPlatformOptions
            {
                DisableDefaultApplicationMenuItems = true
            })
            .UseReactiveUI(cfg =>
            {
            });
        if (renderSettings.RenderingMode.ToUpperInvariant() is "VULKAN" or "OPENGL" or "SOFTWARE")
        {
            builder
                .With(BuildWin32Options(renderSettings))
                .With(new X11PlatformOptions
                {
                    RenderingMode = renderSettings.RenderingMode.ToUpperInvariant() switch
                    {
                        "VULKAN" => [X11RenderingMode.Vulkan],
                        "OPENGL" => [X11RenderingMode.Glx, X11RenderingMode.Egl],
                        "SOFTWARE" => [X11RenderingMode.Software],
                        _ =>
                        [
                            X11RenderingMode.Vulkan, X11RenderingMode.Glx, X11RenderingMode.Egl,
                            X11RenderingMode.Software
                        ]
                    }
                })
                .With(new AvaloniaNativePlatformOptions
                {
                    RenderingMode = renderSettings.RenderingMode.ToUpperInvariant() switch
                    {
                        "VULKAN" => [AvaloniaNativeRenderingMode.Metal],
                        "OPENGL" => [AvaloniaNativeRenderingMode.OpenGl],
                        "SOFTWARE" => [AvaloniaNativeRenderingMode.Software],
                        _ =>
                        [
                            AvaloniaNativeRenderingMode.Metal, AvaloniaNativeRenderingMode.OpenGl,
                            AvaloniaNativeRenderingMode.Software
                        ]
                    }
                });
        }

        return builder;
    }

    private static Win32PlatformOptions BuildWin32Options(RenderSettings renderSettings)
    {
        var win32Options = new Win32PlatformOptions
        {
            //WinUICompositionBackdropCornerRadius = 0.0f,
            RenderingMode = renderSettings.RenderingMode.ToUpperInvariant() switch
            {
                "VULKAN" => [Win32RenderingMode.Vulkan],
                "OPENGL" => [Win32RenderingMode.AngleEgl, Win32RenderingMode.Wgl],
                "SOFTWARE" => [Win32RenderingMode.Software],
                _ =>
                [
                    Win32RenderingMode.Vulkan, Win32RenderingMode.AngleEgl, Win32RenderingMode.Wgl,
                    Win32RenderingMode.Software
                ]
            }
        };
        if (renderSettings.LowLatencyRendering)
        {
            win32Options.CompositionMode =
            [
                Win32CompositionMode.LowLatencyDxgiSwapChain, Win32CompositionMode.WinUIComposition,
                Win32CompositionMode.DirectComposition
            ];
        }

        return win32Options;
    }

    private static int NormalizeGpuMemory(int value) => value switch
    {
        128 or 512 or 1024 => value,
        _ => 256
    };

    private static void ProcessUnhandledExceptions(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        if (ex is not null)
        {
            Core.App.CurrentLogger?.Error(ex, type: EnumLogType.Fatal);
        }
        else
        {
            Core.App.CurrentLogger?.Log($"未处理的非 Exception 对象异常: {e.ExceptionObject}", EnumLogType.Fatal);
        }

        if (!e.IsTerminating)
        {
            return;
        }

        HandleException(ex);
        try
        {
            SentrySdk.CaptureException(ex);
        }
        catch
        {
            // 遥测上报失败不能影响崩溃处理流程
        }
    }

    private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        var exception = e.Exception;

        // Avalonia crashes at Ubuntu.
        if ((exception.Message is not null &&
             exception.Message.Contains("org.freedesktop.DBus.Error.ServiceUnknown")) ||
            exception.InnerExceptions.Any(x =>
                x.Message is not null && x.Message.Contains("org.freedesktop.DBus.Error.ServiceUnknown")))
        {
            e.SetObserved();
            return;
        }

        HandleException(e.Exception);
        e.SetObserved();
    }

    /// <summary>崩溃处理重入保护：崩溃处理过程中再次崩溃时直接输出到控制台，避免无限递归。</summary>
    private static int _crashHandling;

    private static void HandleException(Exception? ex)
    {
        if (ex == null)
        {
            return;
        }

        if (Interlocked.Exchange(ref _crashHandling, 1) != 0)
        {
            try
            {
                System.Console.WriteLine($"[FATAL] Recursive crash during crash handling: {ex.Message}");
            }
            catch
            {
            }

            return;
        }

        ex = ex.InnerException ?? ex;
        try
        {
            System.Console.WriteLine($"""
                                      Unhandled Exception: 
                                      [{ex.GetType()}] {ex.Message}
                                      {ex.StackTrace}
                                      """);
            Core.App.CurrentLogger?.Error(ex, type: EnumLogType.Fatal);
        }
        catch
        {
            // 日志系统故障不应中断崩溃处理
        }

        // 1. 生成崩溃报告（CrashHandler 内部已全面防御）
        string crashLog;
        try
        {
            var crashHandler = new CrashHandler(ex, Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
            CrashHandler.CollectCrashInfo();
            crashLog = crashHandler.GetCrashLog();
            crashHandler.WriteDumpFile(); // 桌面留档（尽力而为）
        }
        catch (Exception crashHandlerEx)
        {
            crashLog = $"""
                        PML 2 Crash Report (fallback)
                        Time: {DateTime.Now}
                        Error: [{ex.GetType()}] {ex.Message}
                        {ex.StackTrace}
                        (CrashHandler failed: {crashHandlerEx.Message})
                        """;
        }

        // 2. 崩溃负载写入文件而不是全部塞进命令行：
        //    完整崩溃日志经 Base64 后可能超过 Windows 命令行 32K 上限，
        //    导致崩溃报告器根本无法启动（崩溃处理静默失败的极端情况）。
        var payloadPath = WriteCrashPayload(crashLog);

        // 3. 启动崩溃报告器；报告器缺失/启动失败时降级为控制台 + 文件留档
        try
        {
            var displayerExe = GetPlatformExe("MEFrpLauncherX.CrashDisplayer");
            if (!File.Exists(displayerExe))
            {
                System.Console.WriteLine(
                    $"[FATAL] CrashDisplayer not found at '{displayerExe}'. Crash log saved to: {payloadPath ?? "(unavailable)"}");
                return;
            }

            // 堆栈摘要截断到 4K（完整堆栈在负载文件中），保证命令行长度始终安全
            var encodedExInfo =
                Base64Encode($"{ex.GetType()}||{ex.Message}||{TruncateForCommandLine(ex.StackTrace)}");
            Process.Start(new ProcessStartInfo
            {
                FileName = displayerExe,
                Arguments = payloadPath is not null
                    ? $"{encodedExInfo} \"{payloadPath}\""
                    : $"{encodedExInfo} {Base64Encode(crashLog)}", // 负载文件写入失败的极端回退
                UseShellExecute = true
            });
        }
        catch (Exception startEx)
        {
            System.Console.WriteLine($"[FATAL] Failed to launch CrashDisplayer: {startEx.Message}");
        }
    }

    /// <summary>将崩溃负载写入 Logs/Crash，失败时回退到系统临时目录，再失败返回 null。</summary>
    private static string? WriteCrashPayload(string crashLog)
    {
        var fileName = $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.log";
        try
        {
            var logPath = Path.Combine(Core.App.StartupPath, "Logs", "Crash");
            Directory.CreateDirectory(logPath);
            var logFile = Path.Combine(logPath, fileName);
            File.WriteAllText(logFile, crashLog);
            return logFile;
        }
        catch
        {
            try
            {
                var tempFile = Path.Combine(Path.GetTempPath(), $"pml2_{fileName}");
                File.WriteAllText(tempFile, crashLog);
                return tempFile;
            }
            catch
            {
                return null;
            }
        }
    }

    private static string? TruncateForCommandLine(string? text, int maxLength = 4096) =>
        text is null || text.Length <= maxLength ? text : text[..maxLength];

    public static string GetPlatformExe(string filename, bool fullPath = false) => fullPath
        ? filename.EndsWith(".exe")
            ? OperatingSystem.IsWindows() ? Path.Combine(filename) : Path.Combine(filename + ".exe")
            : OperatingSystem.IsWindows()
                ? Path.Combine(filename + ".exe")
                : filename
        : OperatingSystem.IsWindows()
            ? Path.Combine(AppContext.BaseDirectory, "Tools", filename + ".exe")
            : Path.Combine(AppContext.BaseDirectory, "Tools", filename);
}

public record StartupData
{
    public int StartProxyId
    {
        get;
        set;
    }

    public string StartProxyName
    {
        get;
        set;
    }
}