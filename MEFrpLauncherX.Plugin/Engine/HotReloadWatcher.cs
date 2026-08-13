using MEFrpLauncherX.Core;

namespace MEFrpLauncherX.Plugin.Engine;

public class HotReloadService
{
    private readonly PluginEngine _engine;
    private FileSystemWatcher? _watcher;
    private DateTime _lastReload = DateTime.MinValue;

    public HotReloadService(PluginEngine engine) => _engine = engine;

    public void Start(string folder)
    {
        _watcher = new FileSystemWatcher(folder, "*.yaml")
        {
            EnableRaisingEvents = true,
            IncludeSubdirectories = true
        };
        _watcher.Changed += (s, e) => Reload(folder, e.ChangeType, e.FullPath);
        _watcher.Created += (s, e) => Reload(folder, e.ChangeType, e.FullPath);
        _watcher.Deleted += (s, e) => Reload(folder, e.ChangeType, e.FullPath);
        _watcher.Renamed += (s, e) => Reload(folder, e.ChangeType, e.FullPath);
        App.CurrentLogger.LogDebug($"插件热重载监听已启动: {folder}", module: EnumLogModule.Plugin);
    }

    private void Reload(string folder, WatcherChangeTypes changeType, string path)
    {
        // 防抖：文件系统事件常常重复触发，1 秒内只重载一次
        if ((DateTime.Now - _lastReload).TotalSeconds < 1) return;
        _lastReload = DateTime.Now;

        App.CurrentLogger.Log($"检测到插件文件{changeType}: {Path.GetFileName(path)}, 正在热重载插件",
            module: EnumLogModule.Plugin);
        try
        {
            _engine.Reload(folder);
        }
        catch (Exception ex)
        {
            App.CurrentLogger.Error(ex, "插件热重载失败", module: EnumLogModule.Plugin);
        }
    }

    public void Stop()
    {
        _watcher?.Dispose();
        App.CurrentLogger.LogDebug("插件热重载监听已停止", module: EnumLogModule.Plugin);
    }
}