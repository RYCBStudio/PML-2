using System.Linq;

namespace MEFrpLauncherX.Core;

/// <summary>
///     26.5.0 起 PML 2 的<b>唯一</b>路径权威（single source of truth）。
///     <para>发布布局（安装目录）：</para>
///     <code>
/// PML 2\                       ← 安装根（InstallRoot）
/// ├─ PML 2.exe                 ← 启动器（版本无关）
/// ├─ launcher.json             ← 版本槽位元数据（current / previous）
/// ├─ data\                     ← 全部可变数据（升级绝不覆盖）
/// │  ├─ Config\                Settings.json / Render.json / device.id / frp\ / Themes\ / Certificates\ / Plugins\
/// │  ├─ Cache\                 startup.json / update_tmp_*.exe / 界面状态
/// │  ├─ Logs\                  yyyy-MM-dd.log / Crash\
/// │  └─ Run\                   运行期下载的可执行文件：mefrpc、lego、CrashDisplayer
/// └─ v26.5.0\                  ← 代码（只读；每个大版本一个目录）
///    ├─ MEFrpLauncherX.exe
///    ├─ Tools\                 splash 等随包分发的辅助程序
///    └─ Resources\ / *.dll / runtimes\ ...
/// </code>
///     <para>
///         <b>关键不变量：代码目录与数据目录彻底分离。</b>升级只替换 <c>vXXX\</c>，
///         <c>data\</c> 原样保留，因此用户的隧道、主题、证书、插件永不丢失；
///         同时也解决了 Linux deb（<c>/opt/pml-2</c>，root 拥有）下运行时不可写的历史问题。
///     </para>
///     <para>
///         <b>环境变量覆盖：</b><c>PML2_INSTALL_ROOT</c> / <c>PML2_DATA_ROOT</c>，
///         启动器与便携模式使用；未设置时按上述布局自动推导。
///     </para>
/// </summary>
public static class AppPaths
{
    /// <summary>数据根目录名（安装根下的 <c>data\</c>）。</summary>
    public const string DataFolderName = "data";

    /// <summary>版本目录名前缀，形如 <c>v26.5.0</c>。</summary>
    public const string VersionFolderPrefix = "v";

    private static readonly Lazy<string> LazyInstallRoot = new(ResolveInstallRoot, isThreadSafe: true);
    private static readonly Lazy<string> LazyVersionDirectory = new(ResolveVersionDirectory, isThreadSafe: true);
    private static readonly Lazy<string> LazyDataRoot = new(ResolveDataRoot, isThreadSafe: true);

    // ---------- 三个根 ----------

    /// <summary>
    ///     安装根目录：包含 <c>PML 2.exe</c>、<c>launcher.json</c>、<c>data\</c> 与各 <c>vXXX\</c> 的目录。
    /// </summary>
    public static string InstallRoot => LazyInstallRoot.Value;

    /// <summary>
    ///     版本（代码）目录：当前运行的 <c>MEFrpLauncherX.exe</c> 所在目录。
    ///     开发态（bin\Debug\net10.0）不处于任何 <c>vXXX\</c> 内时，它等于 <see cref="InstallRoot" />。
    /// </summary>
    public static string VersionDirectory => LazyVersionDirectory.Value;

    /// <summary>数据根目录：安装根下的 <c>data\</c>。</summary>
    public static string DataRoot => LazyDataRoot.Value;

    /// <summary>
    ///     当前是否处于「版本化发布布局」（即代码目录名为 <c>vXXX</c>，与数据目录分离）。
    ///     开发态与旧版单层布局均为 <see langword="false" />。
    /// </summary>
    public static bool IsVersionedLayout => !string.Equals(
        Path.GetFullPath(InstallRoot).TrimEnd(Path.DirectorySeparatorChar),
        Path.GetFullPath(VersionDirectory).TrimEnd(Path.DirectorySeparatorChar),
        PathComparison_OS);

    /// <summary>
    ///     当前版本号（目录名去掉 <c>v</c> 前缀）。开发态回落到 <see cref="App.Version" />。
    /// </summary>
    public static string Version
    {
        get
        {
            var name = Path.GetFileName(VersionDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return IsVersionFolderName(name) ? name[1..] : App.Version;
        }
    }

    // ---------- 数据子目录 ----------

    /// <summary><c>data\Config\</c>：设置、frp 配置、主题、证书、插件。</summary>
    public static string ConfigDirectory => Path.Combine(DataRoot, "Config");

    /// <summary><c>data\Config\frp\</c>：隧道配置文件。</summary>
    public static string FrpConfigDirectory => Path.Combine(ConfigDirectory, "frp");

    /// <summary><c>data\Config\Themes\</c>：用户主题。</summary>
    public static string ThemesDirectory => Path.Combine(ConfigDirectory, "Themes");

    /// <summary><c>data\Config\Themes\selected</c>：当前主题名。</summary>
    public static string SelectedThemeFile => Path.Combine(ThemesDirectory, "selected");

    /// <summary><c>data\Config\Certificates\</c>：ACME 证书。</summary>
    public static string CertificatesDirectory => Path.Combine(ConfigDirectory, "Certificates");

    /// <summary><c>data\Config\Plugins\</c>：插件 YAML（热重载）。</summary>
    public static string PluginsDirectory => Path.Combine(ConfigDirectory, "Plugins");

    /// <summary><c>data\Config\device.id</c>：匿名安装标识。</summary>
    public static string DeviceIdFile => Path.Combine(ConfigDirectory, "device.id");

    /// <summary><c>data\Config\Settings.json</c>。</summary>
    public static string SettingsFile => Path.Combine(ConfigDirectory, "Settings.json");

    /// <summary><c>data\Config\Render.json</c>：渲染设置（Avalonia 初始化前读取）。</summary>
    public static string RenderConfigFile => Path.Combine(ConfigDirectory, "Render.json");

    /// <summary><c>data\Cache\</c>：可随时重建的缓存与升级临时文件。</summary>
    public static string CacheDirectory => Path.Combine(DataRoot, "Cache");

    /// <summary><c>data\Logs\</c>：运行日志。</summary>
    public static string LogsDirectory => Path.Combine(DataRoot, "Logs");

    /// <summary><c>data\Logs\Crash\</c>：崩溃日志。</summary>
    public static string CrashLogsDirectory => Path.Combine(LogsDirectory, "Crash");

    /// <summary><c>data\Run\</c>：运行期下载的可执行文件（mefrpc 客户端、lego、崩溃显示器的解包目标）。</summary>
    public static string RunDirectory => Path.Combine(DataRoot, "Run");

    /// <summary><c>data\Run\mefrpc.exe</c>（Windows）或 <c>data\Run\mefrpc.tar</c>（Unix）。</summary>
    public static string MefrpcFile => Path.Combine(
        RunDirectory,
        OperatingSystem.IsWindows() ? "mefrpc.exe" : "mefrpc.tar");

    /// <summary><c>data\Run\lego[.exe]</c>：Let's Encrypt 客户端。</summary>
    public static string LegoFile => Path.Combine(
        RunDirectory,
        OperatingSystem.IsWindows() ? "lego.exe" : "lego");

    /// <summary><c>vXXX\Tools\</c>：随包分发的辅助程序（splash 等）。</summary>
    public static string ToolsDirectory => Path.Combine(VersionDirectory, "Tools");

    /// <summary><c>vXXX\Resources\</c>：随包分发的静态资源。</summary>
    public static string ResourcesDirectory => Path.Combine(VersionDirectory, "Resources");

    /// <summary><c>vXXX\Assets\</c>：随包分发的 Avalonia 外部资源。</summary>
    public static string AssetsDirectory => Path.Combine(VersionDirectory, "Assets");

    /// <summary>
    ///     Splash 可执行文件。优先取随包分发的 <c>vXXX\Tools\</c>；
    ///     若缺失（例如工具被拆包分发），回退到 <c>data\Run\</c>。
    /// </summary>
    public static string SplashFile
    {
        get
        {
            var shipped = Path.Combine(ToolsDirectory, OperatingSystem.IsWindows() ? "splash.exe" : "splash");
            return File.Exists(shipped) ? shipped : Path.Combine(RunDirectory, OperatingSystem.IsWindows() ? "splash.exe" : "splash");
        }
    }

    /// <summary>
    ///     崩溃显示器的可执行文件路径（不含扩展名调用方自行补）。
    ///     崩溃器随 <c>.pmla</c> 插件包在运行期解包到 <see cref="RunDirectory" />。
    /// </summary>
    public static string CrashDisplayerFile => Path.Combine(
        RunDirectory,
        OperatingSystem.IsWindows() ? "RYCB.MEFrpLauncherX.CrashDisplayer.exe" : "RYCB.MEFrpLauncherX.CrashDisplayer");
    /// <summary>启动器可执行文件（安装根下的 <c>PML 2.exe</c> / <c>PML 2</c>）。</summary>
    public static string LauncherFile => Path.Combine(
        InstallRoot,
        OperatingSystem.IsWindows() ? "PML 2.exe" : "PML 2");

    /// <summary>随包分发的 <c>.pmla</c> 插件包（位于版本目录，只读）。</summary>
    public static string CrashDisplayerPackageFile => Path.Combine(
        VersionDirectory,
        "RYCB.MEFrpLauncherX.CrashDisplayer.pmla");

    /// <summary>
    ///     通过安装根下的启动器重启应用。
    ///     <para>启动器负责挑选当前版本槽位，因此这是唯一可靠的「重启」入口；
    ///     直接启动 <c>MEFrpLauncherX.exe</c> 会绕过版本管理，且在旧写法中还把目录当成了可执行文件。</para>
    ///     <para>开发态（无启动器）回退为直接启动当前进程的可执行文件。</para>
    /// </summary>
    public static void RestartViaLauncher()
    {
        var target = File.Exists(LauncherFile)
            ? LauncherFile
            : Environment.ProcessPath;

        if (string.IsNullOrEmpty(target))
        {
            return;
        }

        var info = new System.Diagnostics.ProcessStartInfo(target)
        {
            UseShellExecute = true
        };

        // 发布态带 --data 显式锚定数据根；开发态不加，避免污染 bin\Debug\data。
        if (File.Exists(LauncherFile) && IsVersionedLayout)
        {
            info.ArgumentList.Add("--data");
            info.ArgumentList.Add(DataRoot);
        }

        System.Diagnostics.Process.Start(info);
    }

    /// <summary>
    ///     将下载完成的 mefrpc 客户端从 <c>*.tmp</c> 落位为正式文件。
    ///     <para>26.5.0 修复：原实现是 <c>MoveTo(x, x)</c> 自我移动后立即 <c>Delete</c>，等价于删掉刚下好的客户端。</para>
    /// </summary>
    public static void InstallMefrpc(LogUtil? logger = null)
    {
        var temp = Path.Combine(RunDirectory, OperatingSystem.IsWindows() ? "mefrpc.exe.tmp" : "mefrpc.tar.tmp");

        try
        {
            if (!File.Exists(temp))
            {
                return;
            }

            File.Move(temp, MefrpcFile, true);
        }
        catch (Exception ex)
        {
            logger?.Error($"安装 mefrpc 客户端失败：{ex.Message}");
        }
    }
    // ---------- 生命周期 ----------

    /// <summary>
    ///     创建全部数据目录。<b>必须在任何配置读取之前调用。</b>
    ///     <para>
    ///         旧版数据的迁移由 <see cref="LegacyLayoutMigrator" /> 单独负责 ——
    ///         它需要日志器就绪后才能记录，因此不在此处执行。
    ///     </para>
    /// </summary>
    public static void EnsureDirectories()
    {
        foreach (var dir in new[]
                 {
                     DataRoot, ConfigDirectory, FrpConfigDirectory, ThemesDirectory,
                     CertificatesDirectory, PluginsDirectory, CacheDirectory, LogsDirectory, RunDirectory
                 })
        {
            TryCreateDirectory(dir);
        }
    }

    private static void TryCreateDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
        }
        catch
        {
            // 只读介质（如只读挂载的安装目录）：由调用方在写入时得到明确异常，此处静默降级。
        }
    }

    // ---------- 推导逻辑 ----------

    private static readonly StringComparison PathComparison_OS =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static string ResolveInstallRoot()
    {
        var overrideRoot = Environment.GetEnvironmentVariable("PML2_INSTALL_ROOT");
        if (!string.IsNullOrWhiteSpace(overrideRoot))
        {
            return Path.GetFullPath(overrideRoot);
        }

        var baseDir = Path.GetFullPath(AppContext.BaseDirectory);
        var dirName = Path.GetFileName(baseDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return IsVersionFolderName(dirName)
            ? Path.GetFullPath(Path.Combine(baseDir, ".."))
            : baseDir;
    }

    private static string ResolveVersionDirectory() =>
        Path.GetFullPath(AppContext.BaseDirectory);

    private static string ResolveDataRoot()
    {
        var overrideRoot = Environment.GetEnvironmentVariable("PML2_DATA_ROOT");
        if (!string.IsNullOrWhiteSpace(overrideRoot))
        {
            return Path.GetFullPath(overrideRoot);
        }

        return Path.Combine(InstallRoot, DataFolderName);
    }

    /// <summary>版本目录名形如 <c>v26.5.0</c> / <c>v26.5.0-beta.1</c>。</summary>
    public static bool IsVersionFolderName(string? name) =>
        !string.IsNullOrEmpty(name)
        && name.Length > 1
        && (name[0] == 'v' || name[0] == 'V')
        && char.IsDigit(name[1]);

    // ---------- 版本槽位管理 ----------

    /// <summary>安装根下的 <c>launcher.json</c> 完整路径。</summary>
    public static string LauncherManifestPath => Path.Combine(InstallRoot, "launcher.json");

    /// <summary>当前版本槽位名（形如 <c>v26.5.0</c>）；旧版单层布局或开发态返回空串。</summary>
    public static string CurrentVersionFolder => IsVersionedLayout
        ? Path.GetFileName(VersionDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        : "";

    /// <summary>launcher.json 中记录的 <c>current</c> 字段；读取失败返回空串。</summary>
    public static string CurrentVersionFolderFromManifest()
    {
        try
        {
            return PML2.Shared.VersionResolver.TryLoadManifest(InstallRoot)?.Current ?? "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>launcher.json 中记录的 <c>previous</c> 字段（回滚点）；无返回空串。</summary>
    public static string PreviousVersionFolder()
    {
        try
        {
            return PML2.Shared.VersionResolver.TryLoadManifest(InstallRoot)?.Previous ?? "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    ///     列出安装根下所有版本目录名，供工具箱展示与「清理旧版本」使用。
    /// </summary>
    public static IReadOnlyList<string> ListInstalledVersions() =>
        PML2.Shared.VersionResolver.ListVersionFolders(InstallRoot);

    /// <summary>
    ///     把版本槽位切换到另一个已安装的版本（写入 launcher.json 的 current）。
    ///     <para>切换后需重启应用才会生效。</para>
    /// </summary>
    public static bool TrySwitchVersion(string folderName, out string? error)
    {
        error = null;

        if (!IsVersionFolderName(folderName))
        {
            error = "目录名不是合法的版本目录";
            return false;
        }

        var executable = Path.Combine(
            InstallRoot,
            folderName,
            OperatingSystem.IsWindows() ? "MEFrpLauncherX.exe" : "MEFrpLauncherX");

        if (!File.Exists(executable))
        {
            error = $"该版本目录下缺少 {Path.GetFileName(executable)}";
            return false;
        }

        try
        {
            var manifest = PML2.Shared.VersionResolver.TryLoadManifest(InstallRoot)
                           ?? new PML2.Shared.LauncherManifest();

            // 把被替换掉的版本记为回滚点，保证切换可逆。
            if (!string.IsNullOrEmpty(manifest.Current)
                && !string.Equals(manifest.Current, folderName, StringComparison.OrdinalIgnoreCase))
            {
                manifest.Previous = manifest.Current;
            }

            manifest.Current = folderName;

            if (!PML2.Shared.VersionResolver.TrySaveManifest(InstallRoot, manifest))
            {
                error = "写入 launcher.json 失败";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    ///     删除一个旧版本目录。
    ///     <para>安全约束：拒绝删除当前正在运行的版本，也拒绝删除 manifest 中记录的 previous（回滚点）。</para>
    /// </summary>
    public static bool TryRemoveVersion(string folderName, out string? error)
    {
        error = null;

        if (!IsVersionFolderName(folderName))
        {
            error = "目录名不是合法的版本目录";
            return false;
        }

        var target = Path.GetFullPath(Path.Combine(InstallRoot, folderName));
        var current = Path.GetFullPath(VersionDirectory);

        if (string.Equals(target, current, PathComparison_OS))
        {
            error = "不能删除当前正在运行的版本";
            return false;
        }

        if (string.Equals(PreviousVersionFolder(), folderName, StringComparison.OrdinalIgnoreCase))
        {
            error = "该版本是回滚点，请先切换版本";
            return false;
        }

        try
        {
            if (Directory.Exists(target))
            {
                Directory.Delete(target, true);
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>安装根下的数据目录体积（字节），用于工具箱展示。</summary>
    public static long GetDataRootSize()
    {
        try
        {
            return Directory.Exists(DataRoot)
                ? Directory.EnumerateFiles(DataRoot, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length)
                : 0;
        }
        catch
        {
            return 0;
        }
    }
}
