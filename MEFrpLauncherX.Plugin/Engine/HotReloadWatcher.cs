using MEFrpLauncherX.Core;

namespace MEFrpLauncherX.Plugin.Engine;

public class HotReloadService
{
    private readonly PluginEngine _engine;
    private readonly List<FileSystemWatcher> _watchers = [];
    private DateTime _lastReload = DateTime.MinValue;

    public HotReloadService(PluginEngine engine) => _engine = engine;

    /// <summary>
    /// 开始监听插件目录。
    /// <para>
    ///     26.5.1：支持监听多个目录（数据目录 + 随包分发的版本目录）。
    ///     每个目录一个 watcher —— <see cref="FileSystemWatcher" /> 只支持单一过滤模式，
    ///     无法同时监听两个不同路径。
    /// </para>
    /// </summary>
    public void Start(IReadOnlyList<string> folders)
    {
        Stop();

        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            var watcher = new FileSystemWatcher(folder, "*.yaml")
            {
                EnableRaisingEvents = true,
                IncludeSubdirectories = true
            };
            watcher.Changed += (s, e) => Reload(folders, e.ChangeType, e.FullPath);
            watcher.Created += (s, e) => Reload(folders, e.ChangeType, e.FullPath);
            watcher.Deleted += (s, e) => Reload(folders, e.ChangeType, e.FullPath);
            watcher.Renamed += (s, e) => Reload(folders, e.ChangeType, e.FullPath);
            _watchers.Add(watcher);
            App.CurrentLogger.LogDebug($"插件热重载监听已启动: {folder}", module: EnumLogModule.Plugin);
        }
    }

    private void Reload(IReadOnlyList<string> folders, WatcherChangeTypes changeType, string path)
    {
        // 防抖：文件系统事件常常重复触发，1 秒内只重载一次
        if ((DateTime.Now - _lastReload).TotalSeconds < 1) return;
        _lastReload = DateTime.Now;

        App.CurrentLogger.Log($"检测到插件文件{changeType}: {Path.GetFileName(path)}, 正在热重载插件",
            module: EnumLogModule.Plugin);
        try
        {
            _engine.Reload(folders);
        }
        catch (Exception ex)
        {
            App.CurrentLogger.Error(ex, "插件热重载失败", module: EnumLogModule.Plugin);
        }
    }

    public void Stop()
    {
        foreach (var watcher in _watchers)
        {
            watcher.Dispose();
        }

        _watchers.Clear();
        App.CurrentLogger.LogDebug("插件热重载监听已停止", module: EnumLogModule.Plugin);
    }
}
