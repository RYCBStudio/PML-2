using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Avalonia;
using Avalonia.Media;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.MEFIntergrated;
using ReactiveUI.Avalonia;
using Sentry;
using static MEFrpLauncherX.Core.StringUtils;

namespace MEFrpLauncherX;

internal sealed class Program
{
    public static Process SplashProcess
    {
        get;
        private set;
    }

    internal static ITransactionTracer StartupTransaction;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        StartupTransaction = SentrySdk.StartTransaction("app.startup", "app.lifecycle");
        System.Console.OutputEncoding = Encoding.UTF8;
        // AssemblyLoadContext.Default.Resolving += (ctx, assemblyName) =>
        // {
        //     var assemblyPath = Path.Combine(AppContext.BaseDirectory, "assemblies", $"{assemblyName.Name}.dll");
        //     return File.Exists(assemblyPath) ? ctx.LoadFromAssemblyPath(assemblyPath) : null;
        // };

        var file = GetPlatformExe(Path.Combine(Core.App.StartupPath, "Tools", "splash"), true);
        if (!DownloadHelper.ValidateFileSimple(file,
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
#endif
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