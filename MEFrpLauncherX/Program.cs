#define AOT
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
using Avalonia.Threading;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.MEFIntegrated;
using ReactiveUI.Avalonia;
using Sentry;
using static MEFrpLauncherX.Core.StringUtils;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

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
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, "System.ComponentModel.DataAnnotations.MaxLengthAttribute",
        "System.ComponentModel.Annotations")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, "System.ComponentModel.TypeDescriptor",
        "System.ComponentModel.TypeConverter")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, "Porta.Pty.PtyOptions", "Porta.Pty")]
// BinaryFormatter（过时，仅作保底；若不存在可能无效）
    [DynamicDependency(DynamicallyAccessedMemberTypes.All,
        "System.Runtime.Serialization.Formatters.Binary.BinaryFormatter", "System.Runtime.Serialization.Formatters")]
    [STAThread]
    public static void Main(string[] args)
    {
        //StartupTransaction = SentrySdk.StartTransaction("app.startup", "app.lifecycle");
        // 1. 定义一个全局唯一的Mutex名称（推荐使用反向域名格式）

        // 2. 尝试创建或打开已存在的命名Mutex
        // 将 mutex 存入 static 字段，不依赖 using var 来维持生命周期
        _mutex = new Mutex(true, $"Global\\{AppPipeName}", out var createdNew);

        PreserveAtStartup();
        if (!createdNew)
        {
            // 已有实例在运行，尝试激活它
            ActivateExistingInstance();
            Environment.Exit(0); // 退出当前进程
            return; // 退出当前进程
        }

        // 启动 Named Pipe 服务器，监听来自第二个实例的"显示窗口"请求
        StartPipeServer();

        System.Console.OutputEncoding = Encoding.UTF8;
        // AssemblyLoadContext.Default.Resolving += (ctx, assemblyName) =>
        // {
        //     var assemblyPath = Path.Combine(AppContext.BaseDirectory, "assemblies", $"{assemblyName.Name}.dll");
        //     return File.Exists(assemblyPath) ? ctx.LoadFromAssemblyPath(assemblyPath) : null;
        // };

        var file = GetPlatformExe(Path.Combine(Core.App.StartupPath, "Tools", "splash"), true);

        if (!File.Exists(file))
        {
            System.Console.WriteLine("\e[33m[WARNING] The Splash file is missing. May need to reinstall.\e[0m");
            Core.App.CurrentLogger.Log("启动画面文件缺失。", EnumLogType.Warn);
        }
        else if (!DownloadHelper.ValidateFileSimple(file,
                     "0180aeb78b091ba60891b5635c218b14|803dff910453f2bcde6d114acb082cd5|" +
                     "f4ee9156ffb37e77f6cdc822d7acbd7c|85a6ad0adbab937851c13345127a3489|" +
                     "f699e44cce5056e68d1c03620ad80016|86efbb015589a98cc5fe10d08c0747ef"))
        {
            System.Console.WriteLine("\e[33m[WARNING] The Splash file has been modified. May need to reinstall.");
            System.Console.WriteLine("[警告] 启动画面文件已被修改。可能需要重新安装。\e[0m");
            Core.App.CurrentLogger.Log("启动画面文件完整性检查失败。", EnumLogType.Warn);
        }
        else
        {
            var p = Process.Start(new ProcessStartInfo
            {
                FileName = file,
                Arguments = $"-v \"{Core.App.Version} ‘{App.Codename}’ \" -b \"{GetBackground()}\""
            });
            SplashProcess = p;
        }
#if !DEBUG
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += ProcessUnhandledExceptions;
        try
        {
#endif
        if (args.Length > 0)
        {
            ProcessStartupArguments(args[0]);
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
        }
        finally
        {
            _pipeServerCts?.Cancel();
            _pipeServerCts?.Dispose();
            _mutex?.ReleaseMutex();
            _mutex?.Close();
        }
#endif
    }


    private static void PreserveAtStartup()
    {
#if AOT
    // 列表里加上你要强制保留的类型（根据之前 dump 的结果）
    Type[] typesToKeep = new[]
    {
        typeof(Porta.Pty.PtyOptions),
        typeof(System.ComponentModel.TypeDescriptor),
        typeof(System.ComponentModel.TypeConverter),
        typeof(System.ComponentModel.DataAnnotations.MaxLengthAttribute),
        //typeof(System.Runtime.Serialization.Formatters.Binary.BinaryFormatter) // 若存在
    };

    foreach (var t in typesToKeep)
    {
        try
        {
            // 触发类型初始化（静态构造函数）
            RuntimeHelpers.RunClassConstructor(t.TypeHandle);
        }
        catch { }

        try
        {
            // 尝试创建实例（如果可实例化）
            if (!t.IsAbstract && !t.IsInterface)
            {
                if (t.IsValueType)
                {
                    // 若为值类型，可尝试 SizeOf
                    try { var s = Marshal.SizeOf(t); } catch { }
                }
                else
                {
                    try { Activator.CreateInstance(t); } catch { }
                }
            }
        }
        catch { }

        try
        {
            // 若为可序列化/可 marshal 的值，尝试 SizeOf，或调用 TypeDescriptor.GetConverter
            try { var c = TypeDescriptor.GetConverter(t); } catch { }
        }
        catch { }
    }

#endif
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

    private static void ActivateExistingInstance()
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
            IntPtr hWnd = process.MainWindowHandle;
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

    public static void ProcessStartupArguments(string arg)
    {
        var data = new StartupData
        {
            StartProxyId = -1,
            StartProxyName = string.Empty
        };
        if (!arg.StartsWith("mefrp://"))
        {
            if (Path.Exists(arg))
            {
                // TODO: PMLA Unpack
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
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        FontManagerOptions options = new();
        if (OperatingSystem.IsLinux())
        {
            options.DefaultFamilyName = "Noto Sans CJK SC";
        }

        options.FontFallbacks =
        [
            new FontFallback
            {
                FontFamily = new FontFamily(new Uri("avares://MEFrpLauncherX.Fonts/Fonts/#HarmonyOS Sans SC"),
                    "Harmony OS Sans SC")
            }
        ];

        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .With(options)
            .With(new Win32PlatformOptions
            {
                WinUICompositionBackdropCornerRadius = 0.0f,
            })
            .With(new SkiaOptions
            {
                MaxGpuResourceSizeBytes = 256 * 1024 * 1024 // 256 MB
            })
            .UseReactiveUI(cfg =>
            {
            });
    }

    private static void ProcessUnhandledExceptions(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        Core.App.CurrentLogger.Error(ex, type: EnumLogType.Fatal);
        if (!e.IsTerminating)
        {
            return;
        }

        HandleException(ex);
        SentrySdk.CaptureException(ex);
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

    private static void HandleException(Exception? ex)
    {
        if (ex == null)
        {
            return;
        }

        ex = ex.InnerException ?? ex;
        if (Core.App.CurrentLogger != null)
        {
            System.Console.WriteLine($"""
                                      Unhandled Exception: 
                                      [{ex.GetType()}] {ex.Message}
                                      {ex.StackTrace}
                                      """);
            Core.App.CurrentLogger.Error(ex, type: EnumLogType.Fatal);
        }
        else
        {
            System.Console.WriteLine($"""
                                      Unhandled Exception: 
                                      [{ex.GetType()}] {ex.Message}
                                      {ex.StackTrace}
                                      """);
        }

        var crashHandler = new CrashHandler(ex, Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
        CrashHandler.CollectCrashInfo();
        var crashLog = crashHandler.GetCrashLog();
        var encodedExInfo = Base64Encode($"{ex.GetType()}||{ex.Message}||{ex.StackTrace}");
        //保存到文件（可选）
        var logPath = Path.Combine(Core.App.StartupPath, "Logs", "Crash");
        Directory.CreateDirectory(logPath);
        var logFile = Path.Combine(logPath, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        File.WriteAllText(logFile, crashLog);
        Process.Start(new ProcessStartInfo
        {
            FileName = GetPlatformExe("MEFrpLauncherX.CrashDisplayer"),
            Arguments = $"{encodedExInfo} {Base64Encode(crashLog)}",
            UseShellExecute = true
        });
    }

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