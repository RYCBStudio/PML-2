using System.Runtime.CompilerServices;

namespace MEFrpLauncherX.Core.Services;

public interface ILogService
{
    void LogInfo(string message, EnumLogModule module = EnumLogModule.Main, string customModuleName = "", 
                [CallerMemberName] string memberName = "",
                [CallerFilePath] string sourceFilePath = "",
                [CallerLineNumber] int sourceLineNumber = 0);
    
    void LogWarning(string message, EnumLogModule module = EnumLogModule.Main, string customModuleName = "",
                   [CallerMemberName] string memberName = "",
                   [CallerFilePath] string sourceFilePath = "",
                   [CallerLineNumber] int sourceLineNumber = 0);
    
    void LogError(Exception ex, string message = "", EnumLogModule module = EnumLogModule.Main, string customModuleName = "",
                 [CallerMemberName] string memberName = "",
                 [CallerFilePath] string sourceFilePath = "",
                 [CallerLineNumber] int sourceLineNumber = 0);
    
    void LogFatal(Exception ex, string message = "", EnumLogModule module = EnumLogModule.Main, string customModuleName = "",
                 [CallerMemberName] string memberName = "",
                 [CallerFilePath] string sourceFilePath = "",
                 [CallerLineNumber] int sourceLineNumber = 0);
    
    void LogDebug(string message, EnumLogModule module = EnumLogModule.Main, string customModuleName = "",
                 [CallerMemberName] string memberName = "",
                 [CallerFilePath] string sourceFilePath = "",
                 [CallerLineNumber] int sourceLineNumber = 0);
}

public class LogService : ILogService
{
    private readonly LogUtil _logger;

    public LogService(LogUtil logger)
    {
        _logger = logger;
    }

    public void LogInfo(string message, EnumLogModule module = EnumLogModule.Main, string customModuleName = "",
                       [CallerMemberName] string memberName = "",
                       [CallerFilePath] string sourceFilePath = "",
                       [CallerLineNumber] int sourceLineNumber = 0)
    {
        _logger.Log(message, EnumLogType.Info, EnumLogPort.Client, module, customModuleName, memberName, sourceFilePath, sourceLineNumber);
    }

    public void LogWarning(string message, EnumLogModule module = EnumLogModule.Main, string customModuleName = "",
                          [CallerMemberName] string memberName = "",
                          [CallerFilePath] string sourceFilePath = "",
                          [CallerLineNumber] int sourceLineNumber = 0)
    {
        _logger.Log(message, EnumLogType.Warn, EnumLogPort.Client, module, customModuleName, memberName, sourceFilePath, sourceLineNumber);
    }

    public void LogError(Exception ex, string message = "", EnumLogModule module = EnumLogModule.Main, string customModuleName = "",
                        [CallerMemberName] string memberName = "",
                        [CallerFilePath] string sourceFilePath = "",
                        [CallerLineNumber] int sourceLineNumber = 0)
    {
        _logger.Error(ex, message, EnumLogType.Error, EnumLogPort.Client, module, customModuleName, memberName, sourceFilePath, sourceLineNumber);
    }

    public void LogFatal(Exception ex, string message = "", EnumLogModule module = EnumLogModule.Main, string customModuleName = "",
                        [CallerMemberName] string memberName = "",
                        [CallerFilePath] string sourceFilePath = "",
                        [CallerLineNumber] int sourceLineNumber = 0)
    {
        _logger.Error(ex, message, EnumLogType.Fatal, EnumLogPort.Client, module, customModuleName, memberName, sourceFilePath, sourceLineNumber);
    }

    public void LogDebug(string message, EnumLogModule module = EnumLogModule.Main, string customModuleName = "",
                        [CallerMemberName] string memberName = "",
                        [CallerFilePath] string sourceFilePath = "",
                        [CallerLineNumber] int sourceLineNumber = 0)
    {
        _logger.LogDebug(message, EnumLogPort.Client, module, customModuleName, memberName, sourceFilePath, sourceLineNumber);
    }
}