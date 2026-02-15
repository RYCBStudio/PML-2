using System.Globalization;
using System.Runtime.CompilerServices;

namespace MEFrpLauncherX.Core;

public class LogUtil : IDisposable
{
    private readonly AsyncLogWriter _asyncWriter;
    private bool _disposed;

    private readonly Dictionary<Enum, string> _translation = new()
    {
        // 原有翻译...
        { EnumLogModule.Main, "主程序" },
        { EnumLogModule.Update, "更新" },
        { EnumLogModule.Net, "网络" },
        { EnumLogModule.Sql, "SQL" },
        { EnumLogModule.Home, "首页" },
        { EnumLogModule.CreateProxy, "创建代理" },
        { EnumLogModule.ManageProxy, "管理代理" },
        { EnumLogModule.NodesMonitoring, "节点监控" },
        { EnumLogModule.About, "关于" },
        { EnumLogModule.Terminal, "终端" },
        { EnumLogPort.Client, "客户端" },
        { EnumLogPort.Server, "服务端" },
        { EnumLogType.Info, "信息" },
        { EnumLogType.Warn, "警告" },
        { EnumLogType.Error, "错误" },
        { EnumLogType.Fatal, "致命错误" },
        { EnumLogType.Debug, "调试" },
    };

    public string LogPath
    {
        get;
    }

    public LogUtil(string logPath)
    {
        LogPath = logPath;
        _asyncWriter = new AsyncLogWriter(logPath);

        InitializeSystemInfo();
    }

    private void InitializeSystemInfo()
    {
        Log("============= 系统信息 =============",
            module: EnumLogModule.Main,
            customModuleName: "初始化");

        try
        {
            Log($"语言: {CultureInfo.CurrentCulture.DisplayName}",
                module: EnumLogModule.Main,
                customModuleName: "初始化");
            Log($"运行目录: {AppContext.BaseDirectory}",
                module: EnumLogModule.Main,
                customModuleName: "初始化");
            Log($"操作系统: {Environment.OSVersion}",
                module: EnumLogModule.Main,
                customModuleName: "初始化");
        }
        catch (Exception ex)
        {
            Log($"获取系统信息失败: {ex.Message}",
                EnumLogType.Warn,
                module: EnumLogModule.Main,
                customModuleName: "初始化");
        }
    }

    /// <summary>
    /// 记录日志
    /// </summary>
    /// <param name="message">要记录的值</param>
    /// <param name="type">类型</param>
    /// <param name="port">端类型</param>
    /// <param name="module">模块</param>
    /// <param name="customModuleName">若<paramref name="module"/>为<see cref="EnumLogModule.Custom"/>(自定义模块), 则需要传入该值。</param>
    /// <param name="memberName">[自动生成] 调用的方法名</param>
    /// <param name="sourceFilePath">[自动生成] 调用的文件名</param>
    /// <param name="sourceLineNumber">[自动生成] 调用该方法的行号</param>
    public void Log(
        object message,
        EnumLogType type = EnumLogType.Info,
        EnumLogPort port = EnumLogPort.Client,
        EnumLogModule module = EnumLogModule.Main,
        string customModuleName = "",
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        if (_disposed)
        {
            return;
        }

        var context = $"{Path.GetFileName(sourceFilePath)}:{memberName}:{sourceLineNumber}";
        var moduleName = module == EnumLogModule.Custom ? customModuleName : _translation[module];

        var logEntry =
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}][{_translation[type]}|{_translation[port]}:{moduleName}][{context}] {message}";

        _asyncWriter.EnqueueLog(logEntry);
    }

    /// <summary>
    /// 记录调试日志，只在Debug模式下起作用。
    /// </summary>
    /// <param name="message">要记录的值</param>
    /// <param name="port">端类型</param>
    /// <param name="module">模块</param>
    /// <param name="customModuleName">若<paramref name="module"/>为<see cref="EnumLogModule.Custom"/>(自定义模块), 则需要传入该值。</param>
    /// <param name="memberName">[自动生成] 调用的方法名</param>
    /// <param name="sourceFilePath">[自动生成] 调用的文件名</param>
    /// <param name="sourceLineNumber">[自动生成] 调用该方法的行号</param>
    public void LogDebug(
        object message,
        EnumLogPort port = EnumLogPort.Client,
        EnumLogModule module = EnumLogModule.Main,
        string customModuleName = "",
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
#if DEBUG
        if (_disposed)
        {
            return;
        }

        var context = $"{Path.GetFileName(sourceFilePath)}:{memberName}:{sourceLineNumber}";
        var moduleName = module == EnumLogModule.Custom ? customModuleName : _translation[module];

        var logEntry =
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}][{_translation[EnumLogType.Debug]}|{_translation[port]}:{moduleName}][{context}] {message}";

        _asyncWriter.EnqueueLog(logEntry);
        Console.WriteLine(
            $"\e[34m[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}]\e[0m\e[36m[{_translation[EnumLogType.Debug]}|{_translation[port]}:{moduleName}]\e[0m\e[35m[{context}]\e[0m {message}");
#endif
    }

    /// <summary>
    /// 记录错误日志
    /// </summary>
    /// <param name="ex">需要记录的异常类型</param>
    /// <param name="message">要记录的附加信息, 于错误记录的第一行展示</param>
    /// <param name="type">类型</param>
    /// <param name="port">端类型</param>
    /// <param name="module">模块</param>
    /// <param name="customModuleName">若<paramref name="module"/>为<see cref="EnumLogModule.Custom"/>(自定义模块), 则需要传入该值。</param>
    /// <param name="memberName">[自动生成] 调用的方法名</param>
    /// <param name="sourceFilePath">[自动生成] 调用的文件名</param>
    /// <param name="sourceLineNumber">[自动生成] 调用该方法的行号</param>
    public void Error(
        Exception ex,
        string message = "",
        EnumLogType type = EnumLogType.Error,
        EnumLogPort port = EnumLogPort.Client,
        EnumLogModule module = EnumLogModule.Main,
        string customModuleName = "",
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        if (_disposed)
        {
            return;
        }

        var context = $"{Path.GetFileName(sourceFilePath)}:{memberName}:{sourceLineNumber}";
        var moduleName = module == EnumLogModule.Custom ? customModuleName : _translation[module];
        Console.WriteLine(
            $"\e[31m[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}]\e[0m\e[31m[{_translation[type]}|{_translation[port]}:{moduleName}]\e[0m\e[31m[{context}]\e[0m 发生错误：[{ex.GetType().Name}] {ex.Message} {message}");

        // 简要错误信息
        var shortLog =
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}][{_translation[type]}|{_translation[port]}:{moduleName}][{context}] 发生错误：[{ex.GetType().Name}] {ex.Message} {message}";
        _asyncWriter.EnqueueLog(shortLog);

        // 详细错误信息
        var detailedLog =
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}][{_translation[type]}|{_translation[port]}:{moduleName}][{context}] \n错误详情:\n类型: [{ex.GetType().FullName}]\n消息: [{ex.Message}]\n堆栈跟踪:\n{GetFullExceptionDetails(ex)}";
        _asyncWriter.EnqueueLog(detailedLog);
    }

    private string GetFullExceptionDetails(Exception ex)
    {
        var details = ex.StackTrace ?? "无堆栈信息";

        // 递归获取内部异常
        var inner = ex.InnerException;
        var depth = 0;
        while (inner != null && depth < 10) // 防止无限递归
        {
            details +=
                $"\n\n内部异常 [{depth + 1}]:\n类型: {inner.GetType().FullName}\n消息: {inner.Message}\n堆栈: {inner.StackTrace}";
            inner = inner.InnerException;
            depth++;
        }

        return details;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _asyncWriter?.Dispose();
        GC.SuppressFinalize(this);
    }
}

public enum EnumLogPort
{
    /// <summary>
    /// 客户端
    /// </summary>
    Client,

    /// <summary>
    /// 服务端
    /// </summary>
    Server,
}

public enum EnumLogModule
{
    /// <summary>
    /// 主模块
    /// </summary>
    Main,

    /// <summary>
    /// 更新
    /// </summary>
    Update,

    /// <summary>
    /// 网络
    /// </summary>
    Net,

    /// <summary>
    /// 数据库
    /// </summary>
    Sql,

    /// <summary>
    /// 自定义
    /// </summary>
    Custom,

    /// <summary>
    /// 页面 - 主页
    /// </summary>
    Home,

    /// <summary>
    /// 页面 - 创建隧道
    /// </summary>
    CreateProxy,

    /// <summary>
    /// 页面 - 管理隧道
    /// </summary>
    ManageProxy,

    /// <summary>
    /// 页面 - 节点监控
    /// </summary>
    NodesMonitoring,

    /// <summary>
    /// 页面 - 关于
    /// </summary>
    About,

    /// <summary>
    /// 页面 - 终端/控制台
    /// </summary>
    Terminal
}

public enum EnumLogType
{
    /// <summary>
    /// 信息级别
    /// </summary>
    Info,

    /// <summary>
    /// 警告级别
    /// </summary>
    Warn,

    /// <summary>
    /// 错误级别
    /// </summary>
    Error,

    /// <summary>
    /// 致命错误级别
    /// </summary>
    Fatal,

    /// <summary>
    /// 调试级别
    /// </summary>
    Debug
}