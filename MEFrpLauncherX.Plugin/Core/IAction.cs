namespace MEFrpLauncherX.Plugin.Core;

public interface IAction
{
    Task ExecuteAsync(ExecutionContext ctx, Dictionary<string, object>? args);
}