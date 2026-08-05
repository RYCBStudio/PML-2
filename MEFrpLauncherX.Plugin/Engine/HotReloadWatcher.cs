namespace MEFrpLauncherX.Plugin.Engine;

public class HotReloadService
{
    private readonly PluginEngine _engine;
    private FileSystemWatcher? _watcher;

    public HotReloadService(PluginEngine engine) => _engine = engine;

    public void Start(string folder)
    {
        _watcher = new FileSystemWatcher(folder, "*.yaml")
        {
            EnableRaisingEvents = true,
            IncludeSubdirectories = true
        };
        _watcher.Changed += (s, e) => { _engine.LoadAll(folder); };
        _watcher.Created += (s, e) => { _engine.LoadAll(folder); };
        _watcher.Deleted += (s, e) => { _engine.LoadAll(folder); };
        _watcher.Renamed += (s, e) => { _engine.LoadAll(folder); };
    }

    public void Stop() => _watcher?.Dispose();
}