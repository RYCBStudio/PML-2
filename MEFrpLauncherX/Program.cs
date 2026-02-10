using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Avalonia;
using Avalonia.Media;
using MEFrpLauncherX.Core;
using Newtonsoft.Json;
using ReactiveUI.Avalonia;

namespace MEFrpLauncherX;

internal sealed class Program
{

    public static Process SplashProcess
    {
        get;
        private set;
    }
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        var p = Process.Start(new ProcessStartInfo()
        {
            FileName = GetPlatformExe(Path.Combine(Core.App.StartupPath, "Tools", "splash")),
            Arguments = $"-v \"{App.Version} “{App.Codename}” \" -b {Path.Combine(Core.App.StartupPath, "Resources", "splash.jpg")}"
        });
        SplashProcess = p;
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

    public static void ProcessStartupArguments(string arg)
    {
        var data = new StartupData
        {
            StartProxyId = -1,
            StartProxyName = string.Empty
        };
        if (!arg.StartsWith("mefrp://"))
        {
            return;
        }

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
            JsonConvert.SerializeObject(data));
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        FontManagerOptions options = new();
        if (OperatingSystem.IsLinux())
        {
            options.DefaultFamilyName = "Noto Sans CJK SC";
            options.FontFallbacks =
            [
                new FontFallback()
                {
                    FontFamily = new(new Uri("avares://MEFrpLauncherX.Fonts/Fonts/#HarmonyOS Sans SC"),
                        "Harmony OS Sans SC")
                }
            ];
        }

        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .With(options)
            .With(new X11PlatformOptions
            {
                UseDBusMenu = true,
            })
            .UseReactiveUI();
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
    }

    private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        HandleException(e.Exception);
        e.SetObserved();
    }

    private static void HandleException(Exception ex)
    {
        if (ex == null)
        {
            return;
        }

        ex = ex.InnerException ?? ex;
        if (Core.App.CurrentLogger != null)
        {
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
        crashHandler.CollectCrashInfo();
        var crashLog = crashHandler.GetCrashLog();
        var encodedExInfo = Base64Encode($"{ex.GetType()}||{ex.Message}||{ex.StackTrace}");
        //保存到文件（可选）
        var logPath = Path.Combine(Core.App.StartupPath, 
            "PML2", "crash_logs");
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

    public static string Base64Encode(string plainText)
    {
        var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
        return Convert.ToBase64String(plainTextBytes);
    }

    public static string GetPlatformExe(string filename)
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(AppContext.BaseDirectory, "Tools", filename + ".exe");
        }

        return Path.Combine(AppContext.BaseDirectory, "Tools", filename);
    }
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