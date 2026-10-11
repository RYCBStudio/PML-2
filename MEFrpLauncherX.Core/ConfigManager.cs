using System.Text.Json;
using MEFrpLauncherX.Core.Models;

// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
#pragma warning disable CS8625 // 无法将 null 字面量转换为非 null 的引用类型。
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 'required' 修饰符或声明为可以为 null。

namespace MEFrpLauncherX.Core;

public static class ConfigManager
{
    /// <summary>
    ///     当前客户端支持的配置 schema 版本。
    ///     <para>改动配置结构（新增项 / 改变字段含义 / 废弃字段）时递增，启动时会自动迁移旧配置。</para>
    ///     <para>v2：<c>UpdateSettings.DownloadSource</c>（更新页下载源）新增。</para>
    ///     <para>v3：<c>TelemetryInstallationId</c>（Cloudflare 匿名使用统计的随机安装标识，26.5.0）新增。</para>
    /// </summary>
    public const int CurrentSchemaVersion = 3;

    private static readonly string ConfigDirectory = AppPaths.ConfigDirectory;

    private static AppConfig _currentConfig;
    private static readonly object _lock = new();

    public static string ConfigPath
    {
        get;
    } = Path.Combine(ConfigDirectory, "Settings.json");

    /// <summary>
    ///     清理旧布局遗留的「更新前配置备份」（26.5.0 起不再生成）。
    ///     <para>
    ///         仅删除历史文件，不做任何合并：分离式布局下 <c>data\Config\Settings.json</c>
    ///         升级时不会被覆盖，配置自然延续，无需还原。
    ///     </para>
    /// </summary>
    private static void CleanupLegacyUpdateBackup()
    {
        foreach (var name in new[] { "Settings.json.bak.update", "KEEP_PROFILE" })
        {
            try
            {
                var path = Path.Combine(ConfigDirectory, name);
                if (File.Exists(path))
                {
                    File.Delete(path);
                    App.CurrentLogger?.Log($"已清理旧版更新备份: {name}",
                        module: EnumLogModule.Custom, customModuleName: "配置管理");
                }
            }
            catch (Exception ex)
            {
                // 清理失败不影响启动
                App.CurrentLogger?.Log($"清理旧版更新备份失败: {name}（{ex.Message}）",
                    module: EnumLogModule.Custom, customModuleName: "配置管理");
            }
        }
    }

    /// <summary>
    ///     获取当前配置（只读）
    /// </summary>
    public static AppConfig CurrentConfig
    {
        get
        {
            lock (_lock)
            {
                return _currentConfig;
            }
        }
    }

    /// <summary>
    ///     初始化配置管理器
    /// </summary>
    public static void Initialize()
    {
        if (!Directory.Exists(ConfigDirectory))
        {
            Directory.CreateDirectory(ConfigDirectory);
        }

        if (!File.Exists(ConfigPath))
        {
            _currentConfig = CreateDefaultConfig();
            _currentConfig.SchemaVersion = CurrentSchemaVersion;
            SaveConfig();
        }
        else
        {
            LoadConfig();
        }

        // 无论走哪条分支，都做一次 schema 校验，确保 _currentConfig 结构完整、取值合法。
        EnsureConfigSchemaLocked();
    }

    /// <summary>
    ///     加载配置文件
    /// </summary>
    public static void LoadConfig()
    {
        try
        {
            lock (_lock)
            {
                var json = File.ReadAllText(ConfigPath);
                _currentConfig =
                    JsonSerializer.Deserialize<AppConfig>(json, App.AppJsonSerializerContext.AppConfig);

                // 26.5.0：更新前的配置备份 / 还原逻辑移除。
                // 旧布局下更新会原地覆盖程序目录，Settings.json 可能被新版本重写，
                // 因此才需要先备份再于下次启动合并回来。分离式布局下代码在 vXXX\、
                // 配置在 data\，升级不触碰 data\ —— 备份文件不会再产生，历史遗留的
                // Settings.json.bak.update 也一并在此清理掉。
                CleanupLegacyUpdateBackup();
            }
        }
        catch (Exception ex)
        {
            // 配置损坏（JSON 解析失败/文件截断）时先留档再重建，便于事后追溯与人工恢复。
            BackupCorruptedConfig();
            // 如果加载失败，使用默认配置
            lock (_lock)
            {
                _currentConfig = CreateDefaultConfig();
            }

            App.CurrentLogger?.Log($"加载配置文件失败，使用默认配置: {ex.Message}",
                module: EnumLogModule.Custom, customModuleName: "配置管理");
        }
    }

    /// <summary>
    ///     将损坏的配置文件复制为 Settings.json.corrupt-yyyyMMddHHmmss 备份，失败时静默忽略。
    /// </summary>
    private static void BackupCorruptedConfig()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                return;
            }

            var backupPath = Path.Combine(ConfigDirectory,
                $"Settings.json.corrupt-{DateTime.Now:yyyyMMddHHmmss}");
            File.Copy(ConfigPath, backupPath, true);
            App.CurrentLogger?.Log($"已备份损坏的配置文件到: {backupPath}",
                module: EnumLogModule.Custom, customModuleName: "配置管理");
        }
        catch
        {
            // 备份失败不影响默认配置重建
        }
    }

    #region 配置 Schema 校验与迁移

    private static readonly string[] SkinValues = ["Mica", "AcrylicBlur", "Acrylic", "Blur", "Transparent", "None"];
    private static readonly string[] ThemeValues = ["Dark", "Light", "System"];
    private static readonly string[] CaptchaModeValues = ["implicit", "explicit", "nosense", "browser"];
    private static readonly string[] DownloadSourceValues = ["TPCA", "Official"];
    private static readonly string[] TerminalEngineValues = ["Original", "XTerm"];
    private static readonly string[] LanguageValues = ["zh-CN", "en-US", "zh-Hant"];
    private static readonly string[] UpdateChannelValues = ["Preview", "Stable"];
    private static readonly string[] UpdateMethodValues = ["ds", "dd", "md"];

    /// <summary>
    ///     更新页下载源取值（26.4）。
    ///     须与 <see cref="Services.GitHubUpdateSources.All" /> 保持一致：TPCA 走 Alist CDN，
    ///     其余三项走 GitHub Release（GitHub 直连 / gh-proxy 镜像 / moeyy 镜像）。
    /// </summary>
    private static readonly string[] UpdateDownloadSourceValues =
        ["TPCA", "GitHub", "GitHubGhProxy", "GitHubMoeyy"];

    private static readonly string[] UpdateOldVersionValues = UpdateSettings.UpdateOldVersionValues.All;

    private static readonly string[] SplashStyleValues = ["default", "dark", "minimal"];
    private static readonly string[] HomeLayoutValues = ["classic", "simple"];
    private static readonly string[] FloatPositionValues = ["lt", "rt", "lb", "rb", "ct", "cb"];
    private static readonly string[] StretchValues = ["None", "Stretch", "Uniform", "UniformToFill", "disabled"];
    private static readonly string[] TileModeValues = ["disabled", "None", "Tile", "FlipX", "FlipY", "FlipXY"];

    /// <summary>
    ///     本次运行是否已做过 schema 校验。
    ///     <para>
    ///         防止 <see cref="EnsureConfigSchema" /> 被重复调用时反复写盘；
    ///         但一旦校验触发了保存（<see cref="SaveConfig" /> 会复位该标记），就允许再校验一轮，
    ///         以覆盖「迁移之后又被写回非法值」的极端情况，同时保证不会无限循环。
    ///     </para>
    /// </summary>
    private static bool _schemaChecked;

    /// <summary>
    ///     校验当前配置是否符合最新 schema；不符合则自动迁移。
    ///     <para>
    ///         迁移原则：只补齐、不覆盖。缺失的字段补默认值，非法的取值回落到默认值，
    ///         用户设置过的合法值一律原样保留（不会因为升级而丢失配置）。
    ///     </para>
    /// </summary>
    public static void EnsureConfigSchema()
    {
        lock (_lock)
        {
            EnsureConfigSchemaLocked();
        }
    }

    private static void EnsureConfigSchemaLocked()
    {
        if (_currentConfig is null)
        {
            _currentConfig = CreateDefaultConfig();
            _currentConfig.SchemaVersion = CurrentSchemaVersion;
            return;
        }

        if (_schemaChecked)
        {
            return;
        }

        _schemaChecked = true;

        var actionCount = NormalizeToLatestSchema(_currentConfig);

        if (_currentConfig.SchemaVersion != CurrentSchemaVersion)
        {
            App.CurrentLogger?.Log(
                $"配置 schema 版本过旧（v{_currentConfig.SchemaVersion} -> v{CurrentSchemaVersion}），已自动迁移 {actionCount} 处。",
                module: EnumLogModule.Custom, customModuleName: "配置管理");
            _currentConfig.SchemaVersion = CurrentSchemaVersion;
            actionCount++;
        }

        if (actionCount > 0)
        {
            SaveConfig();
        }
    }

    #endregion

    /// <summary>
    ///     把配置归一化到最新 schema：补齐缺失的嵌套对象/集合、剔除废弃字段、修正非法取值。
    /// </summary>
    /// <returns>实际修正的项数（0 表示配置已完全符合最新 schema）。</returns>
    internal static int NormalizeToLatestSchema(AppConfig cfg)
    {
        var changed = 0;

        // 以「默认配置」作为最新 schema 的权威默认值，避免默认值在两处维护而漂移。
        var d = CreateDefaultConfig();

        // ---------- 1. 补齐缺失的嵌套对象（旧配置可能整段缺失，或 JSON 中显式为 null） ----------
        if (cfg.UpdateSettings is null)
        {
            cfg.UpdateSettings = d.UpdateSettings;
            changed++;
        }

        if (cfg.BackgroundSettings is null)
        {
            cfg.BackgroundSettings = d.BackgroundSettings;
            changed++;
        }

        if (cfg.HomeSettings is null)
        {
            cfg.HomeSettings = d.HomeSettings;
            changed++;
        }

        if (cfg.PMSettings is null)
        {
            cfg.PMSettings = d.PMSettings;
            changed++;
        }

        if (cfg.CreateProxyDefaults is null)
        {
            cfg.CreateProxyDefaults = d.CreateProxyDefaults;
            changed++;
        }

        // 集合类型：null 会破坏调用方（如 AutoLaunchProxies.Count），统一补空集合。
        if (cfg.AutoLaunchProxies is null)
        {
            cfg.AutoLaunchProxies = [];
            changed++;
        }

        if (cfg.ProxyTemplates is null)
        {
            cfg.ProxyTemplates = [];
            changed++;
        }

        // ---------- 2. 枚举型字符串：非法值回落到默认值，并把大小写规范化为标准写法 ----------
        changed += NormalizeChoice(() => cfg.Skin, v => cfg.Skin = v, SkinValues, d.Skin);
        changed += NormalizeChoice(() => cfg.Theme, v => cfg.Theme = v, ThemeValues, d.Theme);
        changed += NormalizeChoice(() => cfg.CaptchaMode, v => cfg.CaptchaMode = v, CaptchaModeValues, d.CaptchaMode);
        changed += NormalizeChoice(() => cfg.DownloadSource, v => cfg.DownloadSource = v, DownloadSourceValues,
            d.DownloadSource);
        changed += NormalizeChoice(() => cfg.TerminalEngineType, v => cfg.TerminalEngineType = v,
            TerminalEngineValues, d.TerminalEngineType);
        changed += NormalizeChoice(() => cfg.Language, v => cfg.Language = v, LanguageValues, d.Language);
        changed += NormalizeChoice(() => cfg.UpdateSettings.Channel, v => cfg.UpdateSettings.Channel = v,
            UpdateChannelValues, d.UpdateSettings.Channel);
        changed += NormalizeChoice(() => cfg.UpdateSettings.Method, v => cfg.UpdateSettings.Method = v,
            UpdateMethodValues, d.UpdateSettings.Method);
        changed += NormalizeChoice(() => cfg.UpdateSettings.DownloadSource,
            v => cfg.UpdateSettings.DownloadSource = v,
            UpdateDownloadSourceValues, d.UpdateSettings.DownloadSource);
        changed += NormalizeChoice(() => cfg.UpdateSettings.KeepOldVersion,
            v => cfg.UpdateSettings.KeepOldVersion = v,
            UpdateOldVersionValues, d.UpdateSettings.KeepOldVersion);
        changed += NormalizeChoice(() => cfg.SplashStyle, v => cfg.SplashStyle = v, SplashStyleValues, d.SplashStyle);
        changed += NormalizeChoice(() => cfg.HomeSettings.Layout, v => cfg.HomeSettings.Layout = v, HomeLayoutValues,
            d.HomeSettings.Layout);
        changed += NormalizeChoice(() => cfg.PMSettings.Position, v => cfg.PMSettings.Position = v,
            FloatPositionValues, d.PMSettings.Position);
        changed += NormalizeChoice(() => cfg.BackgroundSettings.Stretch, v => cfg.BackgroundSettings.Stretch = v,
            StretchValues, d.BackgroundSettings.Stretch);
        changed += NormalizeChoice(() => cfg.BackgroundSettings.TileMode, v => cfg.BackgroundSettings.TileMode = v,
            TileModeValues, d.BackgroundSettings.TileMode);

        // ---------- 3. 自由字符串：仅补 null / 空白，不改变用户输入 ----------
        if (cfg.TerminalCli.IsNullOrEmpty())
        {
            cfg.TerminalCli = CliUtils.GetOSSpeceficDefaultCli();
            changed++;
        }

        if (cfg.AccentColor is null)
        {
            cfg.AccentColor = string.Empty;
            changed++;
        }

        // 遥测安装标识：仅补 null 为空串（不在此生成 UUID —— 关闭遥测时不得生成 installation_id），
        // 也不校验格式：损坏值留给 TelemetryService 在「已启用遥测」时重新生成。
        if (cfg.TelemetryInstallationId is null)
        {
            cfg.TelemetryInstallationId = string.Empty;
            changed++;
        }

        if (cfg.SplashCustomImagePath is null)
        {
            cfg.SplashCustomImagePath = string.Empty;
            changed++;
        }

        if (cfg.BackgroundSettings.BackgroundImage is null)
        {
            cfg.BackgroundSettings.BackgroundImage = d.BackgroundSettings.BackgroundImage;
            changed++;
        }

        // ---------- 4. 数值范围：越界值回落到默认值（与设置页 Slider / FANumberBox 范围一致） ----------
        changed += NormalizeRange(() => cfg.ParallelCount, v => cfg.ParallelCount = v, 1, 512, d.ParallelCount);
        changed += NormalizeRange(() => cfg.ExpireDays, v => cfg.ExpireDays = v, 1, 366, d.ExpireDays);
        changed += NormalizeRange(() => cfg.AnimationLevel, v => cfg.AnimationLevel = v, 0, 2, d.AnimationLevel);
        changed += NormalizeRange(() => cfg.BackgroundSettings.LayerOpacity,
            v => cfg.BackgroundSettings.LayerOpacity = v, 0d, 1d, d.BackgroundSettings.LayerOpacity);
        changed += NormalizeRange(() => cfg.PMSettings.Opacity, v => cfg.PMSettings.Opacity = v, 0.5d, 1d,
            d.PMSettings.Opacity);

        return changed;
    }

    /// <summary>
    ///     校验枚举型字符串：非法值写入默认值，合法但大小写不标准时改写为标准写法。
    /// </summary>
    private static int NormalizeChoice(Func<string?> get, Action<string> set, string[] allowed, string fallback)
    {
        var current = get();
        if (!string.IsNullOrWhiteSpace(current))
        {
            foreach (var candidate in allowed)
            {
                if (!string.Equals(candidate, current, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // 大小写已是标准写法则视为未改动
                return string.Equals(candidate, current, StringComparison.Ordinal)
                    ? 0
                    : AssignAndCount(set, candidate);
            }
        }

        return AssignAndCount(set, fallback);
    }

    /// <summary>校验整数范围，越界时回落到默认值。</summary>
    private static int NormalizeRange(Func<int> get, Action<int> set, int min, int max, int fallback)
    {
        var current = get();
        return current >= min && current <= max ? 0 : AssignAndCount(set, fallback);
    }

    /// <summary>校验浮点范围，越界时回落到默认值（同时排除 NaN / Infinity）。</summary>
    private static int NormalizeRange(Func<double> get, Action<double> set, double min, double max, double fallback)
    {
        var current = get();
        return double.IsFinite(current) && current >= min && current <= max ? 0 : AssignAndCount(set, fallback);
    }

    private static int AssignAndCount<T>(Action<T> set, T value)
    {
        set(value);
        return 1;
    }

    /// <summary>
    ///     写入磁盘前清理 JSON（当前为直通）。
    ///     <para>
    ///         约定：Settings.json <b>必须是纯 JSON</b>。System.Text.Json 默认不接受 JSON 注释，
    ///         一旦写入 `//` 注释，<see cref="LoadConfig" /> 会把文件判为「配置损坏」并重建默认配置
    ///         （等于静默清空用户设置）。因此 schema 版本只通过
    ///         <see cref="AppConfig.SchemaVersion" /> 字段持久化，不额外写任何标记。
    ///     </para>
    /// </summary>
    private static string SanitizeConfigJson(string json) => json;

    private static void MergeConfig(AppConfig source, ref AppConfig target)
    {
        if (target == null || source == null)
        {
            return;
        }

        App.CurrentLogger?.Log($"正在合并配置项 PrivacyAgreed: {target.PrivacyAgreed} -> {source.PrivacyAgreed}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (!target.PrivacyAgreed && source.PrivacyAgreed)
        {
            target.PrivacyAgreed = source.PrivacyAgreed;
        }

        App.CurrentLogger?.Log(
            $"正在合并配置项 IsTelemetryEnabled: {target.IsTelemetryEnabled} -> {source.IsTelemetryEnabled}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (!target.IsTelemetryEnabled && source.IsTelemetryEnabled)
        {
            target.IsTelemetryEnabled = source.IsTelemetryEnabled;
        }

        // 遥测安装标识：跟随用户同意一起保留，保证「同一安装长期复用同一个 installation_id」。
        // 仅在 target 仍为空时采用备份值，避免覆盖用户切换账号 / 重置后新生成的标识；
        // 未同意遥测时一律不同步，避免在关闭状态下把标识带回。
        App.CurrentLogger?.Log(
            $"正在合并配置项 TelemetryInstallationId: {target.TelemetryInstallationId} -> {source.TelemetryInstallationId}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (target.TelemetryInstallationId.IsNullOrEmpty() && source.IsTelemetryEnabled &&
            !source.TelemetryInstallationId.IsNullOrEmpty())
        {
            target.TelemetryInstallationId = source.TelemetryInstallationId;
        }

        App.CurrentLogger?.Log($"正在合并配置项 Skin: {target.Skin} -> {source.Skin}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (string.IsNullOrEmpty(target.Skin) && !string.IsNullOrEmpty(source.Skin))
        {
            target.Skin = source.Skin;
        }

        App.CurrentLogger?.Log(
            $"正在合并配置项 KickWithoutDisable: {target.KickWithoutDisable} -> {source.KickWithoutDisable}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (!target.KickWithoutDisable && source.KickWithoutDisable)
        {
            target.KickWithoutDisable = source.KickWithoutDisable;
        }

        App.CurrentLogger?.Log(
            $"正在合并配置项 HideInsteadOfClose: {target.HideInsteadOfClose} -> {source.HideInsteadOfClose}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (!target.HideInsteadOfClose && source.HideInsteadOfClose)
        {
            target.HideInsteadOfClose = source.HideInsteadOfClose;
        }

        App.CurrentLogger?.Log($"正在合并配置项 ParallelDownload: {target.ParallelDownload} -> {source.ParallelDownload}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (!target.ParallelDownload && source.ParallelDownload)
        {
            target.ParallelDownload = source.ParallelDownload;
        }

        App.CurrentLogger?.Log($"正在合并配置项 ParallelCount: {target.ParallelCount} -> {source.ParallelCount}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (target.ParallelCount == 0 && source.ParallelCount != 0)
        {
            target.ParallelCount = source.ParallelCount;
        }

        App.CurrentLogger?.Log($"正在合并配置项 AutoStartup: {target.AutoStartup} -> {source.AutoStartup}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (!target.AutoStartup && source.AutoStartup)
        {
            target.AutoStartup = source.AutoStartup;
        }

        App.CurrentLogger?.Log($"正在合并配置项 AutoLaunch: {target.AutoLaunch} -> {source.AutoLaunch}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (!target.AutoLaunch && source.AutoLaunch)
        {
            target.AutoLaunch = source.AutoLaunch;
        }

        App.CurrentLogger?.Log(
            $"正在合并配置项 AutoLaunchProxies: {target.AutoLaunchProxies} -> {source.AutoLaunchProxies}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (target.AutoLaunchProxies != null && source.AutoLaunchProxies is { Count: > 0 } && target.AutoLaunchProxies.Count == 0)
        {
            target.AutoLaunchProxies = source.AutoLaunchProxies;
        }

        App.CurrentLogger?.Log($"正在合并配置项 ExpireDays: {target.ExpireDays} -> {source.ExpireDays}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (target.ExpireDays == 0 && source.ExpireDays != 0)
        {
            target.ExpireDays = source.ExpireDays;
        }

        App.CurrentLogger?.Log(
            $"正在合并配置项 DoNotShowSuccessMsg: {target.DoNotShowSuccessMsg} -> {source.DoNotShowSuccessMsg}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (!target.DoNotShowSuccessMsg && source.DoNotShowSuccessMsg)
        {
            target.DoNotShowSuccessMsg = source.DoNotShowSuccessMsg;
        }

        App.CurrentLogger?.Log($"正在合并配置项 Theme: {target.Theme} -> {source.Theme}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (string.IsNullOrEmpty(target.Theme) && !string.IsNullOrEmpty(source.Theme))
        {
            target.Theme = source.Theme;
        }

        App.CurrentLogger?.Log($"正在合并配置项 AccentColor: {target.AccentColor} -> {source.AccentColor}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        target.AccentColor = source.AccentColor;

        App.CurrentLogger?.Log($"正在合并配置项 CaptchaMode: {target.CaptchaMode} -> {source.CaptchaMode}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (string.IsNullOrEmpty(target.CaptchaMode) && !string.IsNullOrEmpty(source.CaptchaMode))
        {
            target.CaptchaMode = source.CaptchaMode;
        }

        App.CurrentLogger?.Log($"正在合并配置项 DownloadSource: {target.DownloadSource} -> {source.DownloadSource}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (string.IsNullOrEmpty(target.DownloadSource) && !string.IsNullOrEmpty(source.DownloadSource))
        {
            target.DownloadSource = source.DownloadSource;
        }

        App.CurrentLogger?.Log($"正在合并配置项 UpdateSettings: {target.UpdateSettings} -> {source.UpdateSettings}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (target.UpdateSettings == null && source.UpdateSettings != null)
        {
            target.UpdateSettings = source.UpdateSettings;
        }
        else if (target.UpdateSettings != null && source.UpdateSettings != null)
        {
            MergeUpdateSettings(target.UpdateSettings, source.UpdateSettings);
        }

        App.CurrentLogger?.Log(
            $"正在合并配置项 BackgroundSettings: {target.BackgroundSettings} -> {source.BackgroundSettings}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (target.BackgroundSettings == null && source.BackgroundSettings != null)
        {
            target.BackgroundSettings = source.BackgroundSettings;
        }
        else if (target.BackgroundSettings != null && source.BackgroundSettings != null)
        {
            MergeBackgroundSettings(target.BackgroundSettings, source.BackgroundSettings);
        }

        App.CurrentLogger?.Log($"正在合并配置项 PMSettings: {target.PMSettings} -> {source.PMSettings}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (target.PMSettings == null && source.PMSettings != null)
        {
            target.PMSettings = source.PMSettings;
        }
        else if (target.PMSettings != null && source.PMSettings != null)
        {
            MergePMSettings(ref target, source.PMSettings);
        }

        App.CurrentLogger?.Log($"正在合并配置项 HomeSettings: {target.HomeSettings} -> {source.HomeSettings}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (target.HomeSettings == null && source.HomeSettings != null)
        {
            target.HomeSettings = source.HomeSettings;
        }
        else if (target.HomeSettings != null && source.HomeSettings != null)
        {
            MergeHomeSettings(target.HomeSettings, source.HomeSettings);
        }
    }

    private static void MergePMSettings(ref AppConfig target, PFSConfig source)
    {
        App.CurrentLogger?.Log($"正在合并配置项 PMSettings>Position: {target.PMSettings.Position} -> {source.Position}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (target.PMSettings.Position != source.Position)
        {
            target.PMSettings.Position = source.Position;
        }

        App.CurrentLogger?.Log($"正在合并配置项 PMSettings>Enabled: {target.PMSettings.Enabled} -> {source.Enabled}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (target.PMSettings.Enabled != source.Enabled)
        {
            target.PMSettings.Enabled = source.Enabled;
        }
    }

    private static void MergeUpdateSettings(UpdateSettings source, UpdateSettings target)
    {
        App.CurrentLogger?.Log($"正在合并配置项 Update>AutoCheck: {source.AutoCheck} -> {target.AutoCheck}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (!source.AutoCheck && target.AutoCheck)
        {
            source.AutoCheck = target.AutoCheck;
        }

        App.CurrentLogger?.Log($"正在合并配置项 Update>Method: {source.Method} -> {target.Method}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (source.Method != target.Method)
        {
            source.Method = target.Method;
        }

        App.CurrentLogger?.Log($"正在合并配置项 Update>Channel: {source.Channel} -> {target.Channel}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (source.Channel != target.Channel)
        {
            source.Channel = target.Channel;
        }

        // 下载源：旧配置没有该字段（空值）时采用用户已保存的取值
        App.CurrentLogger?.Log(
            $"正在合并配置项 Update>DownloadSource: {source.DownloadSource} -> {target.DownloadSource}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (string.IsNullOrEmpty(source.DownloadSource) && !string.IsNullOrEmpty(target.DownloadSource))
        {
            source.DownloadSource = target.DownloadSource;
        }

        // 旧版本目录策略：旧配置没有该字段（空值）时采用用户已保存的取值；
        // 已显式选择过（Keep/Discard）的用户不被默认值 Ask 覆盖。
        App.CurrentLogger?.Log(
            $"正在合并配置项 Update>KeepOldVersion: {source.KeepOldVersion} -> {target.KeepOldVersion}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (string.IsNullOrEmpty(source.KeepOldVersion) && !string.IsNullOrEmpty(target.KeepOldVersion))
        {
            source.KeepOldVersion = target.KeepOldVersion;
        }

        // 仅当旧配置没有该字段时（bool 不可区分「缺失」与 false，这里以默认 true 为准，
        // 只有用户在旧配置里显式关过才保持关闭）才写入默认开启。
        App.CurrentLogger?.Log(
            $"正在合并配置项 Update>NotifyRedundantVersion: {source.NotifyRedundantVersion} -> {target.NotifyRedundantVersion}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (source.NotifyRedundantVersion != target.NotifyRedundantVersion)
        {
            source.NotifyRedundantVersion = target.NotifyRedundantVersion;
        }
    }

    private static void MergeBackgroundSettings(BackgroundSettings source, BackgroundSettings target)
    {
        App.CurrentLogger?.Log(
            $"正在合并配置项 Background>BackgroundImage: {source.BackgroundImage} -> {target.BackgroundImage}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (string.IsNullOrEmpty(source.BackgroundImage) && !string.IsNullOrEmpty(target.BackgroundImage))
        {
            source.BackgroundImage = target.BackgroundImage;
        }

        App.CurrentLogger?.Log($"正在合并配置项 Background>Stretch: {source.Stretch} -> {target.Stretch}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (string.IsNullOrEmpty(source.Stretch) && !string.IsNullOrEmpty(target.Stretch))
        {
            source.Stretch = target.Stretch;
        }

        App.CurrentLogger?.Log($"正在合并配置项 Background>TileMode: {source.TileMode} -> {target.TileMode}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (string.IsNullOrEmpty(source.TileMode) && !string.IsNullOrEmpty(target.TileMode))
        {
            source.TileMode = target.TileMode;
        }

        App.CurrentLogger?.Log($"正在合并配置项 Background>LayerOpacity: {source.LayerOpacity} -> {target.LayerOpacity}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (source.LayerOpacity == 0 && target.LayerOpacity != 0)
        {
            source.LayerOpacity = target.LayerOpacity;
        }

        App.CurrentLogger?.Log(
            $"正在合并配置项 Background>ShouldFillTitleBar: {source.ShouldFillTitleBar} -> {target.ShouldFillTitleBar}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (!source.ShouldFillTitleBar && target.ShouldFillTitleBar)
        {
            source.ShouldFillTitleBar = target.ShouldFillTitleBar;
        }
    }

    private static void MergeHomeSettings(HomeConfig source, HomeConfig target)
    {
        App.CurrentLogger?.Log(
            $"正在合并配置项 Home>Layout: {source.Layout} -> {target.Layout}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        // 旧配置没有 Layout（空/未知值）时采用用户已保存的布局；
        // 双方都为空时保持 HomeConfig 默认值 classic。
        if (string.IsNullOrEmpty(source.Layout) && !string.IsNullOrEmpty(target.Layout))
        {
            source.Layout = target.Layout;
        }

        App.CurrentLogger?.Log($"正在合并配置项 Home>ShowStatistics: {source.ShowStatistics} -> {target.ShowStatistics}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (!source.ShowStatistics && target.ShowStatistics)
        {
            source.ShowStatistics = target.ShowStatistics;
        }

        App.CurrentLogger?.Log($"正在合并配置项 Home>ShowUserInfo: {source.ShowUserInfo} -> {target.ShowUserInfo}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (!source.ShowUserInfo && target.ShowUserInfo)
        {
            source.ShowUserInfo = target.ShowUserInfo;
        }

        App.CurrentLogger?.Log($"正在合并配置项 Home>ShowSystemInfo: {source.ShowSystemInfo} -> {target.ShowSystemInfo}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (!source.ShowSystemInfo && target.ShowSystemInfo)
        {
            source.ShowSystemInfo = target.ShowSystemInfo;
        }

        App.CurrentLogger?.Log(
            $"正在合并配置项 Home>ShowSystemNotice: {source.ShowSystemNotice} -> {target.ShowSystemNotice}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (!source.ShowSystemNotice && target.ShowSystemNotice)
        {
            source.ShowSystemNotice = target.ShowSystemNotice;
        }

        App.CurrentLogger?.Log(
            $"正在合并配置项 Home>ShowSoftwareNotice: {source.ShowSoftwareNotice} -> {target.ShowSoftwareNotice}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (!source.ShowSoftwareNotice && target.ShowSoftwareNotice)
        {
            source.ShowSoftwareNotice = target.ShowSoftwareNotice;
        }
    }

    /// <summary>
    ///     保存当前配置到文件
    /// </summary>
    public static void SaveConfig()
    {
        try
        {
            lock (_lock)
            {
                // 每次落盘都写回当前 schema 版本，保证文件自身即描述当前结构。
                _currentConfig.SchemaVersion = CurrentSchemaVersion;
                var json = JsonSerializer.Serialize(_currentConfig, App.AppJsonSerializerContext.AppConfig);
                File.WriteAllText(ConfigPath, SanitizeConfigJson(json));

                // 允许下一次启动（或下一次 EnsureConfigSchema）重新校验一轮。
                _schemaChecked = false;
            }
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Log($"保存配置文件失败: {ex.Message}",
                module: EnumLogModule.Custom, customModuleName: "配置管理");
        }
    }

    /// <summary>
    ///     异步保存当前配置到文件
    /// </summary>
    public static async Task SaveConfigAsync()
    {
        try
        {
            string json;
            lock (_lock)
            {
                // 与 SaveConfig 保持一致：写回 schema 版本并允许下次重新校验。
                _currentConfig.SchemaVersion = CurrentSchemaVersion;
                json = SanitizeConfigJson(
                    JsonSerializer.Serialize(_currentConfig, App.AppJsonSerializerContext.AppConfig));
                _schemaChecked = false;
            }

            await File.WriteAllTextAsync(ConfigPath, json);
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Log($"异步保存配置文件失败: {ex.Message}",
                module: EnumLogModule.Custom, customModuleName: "配置管理");
        }
    }

    /// <summary>
    ///     更新配置并保存
    /// </summary>
    /// <param name="updateAction">更新配置的回调函数</param>
    public static void UpdateConfig(Action<AppConfig> updateAction)
    {
        lock (_lock)
        {
            updateAction?.Invoke(_currentConfig);
            SaveConfig();
        }
    }

    /// <summary>
    ///     异步更新配置并保存
    /// </summary>
    /// <param name="updateAction">更新配置的回调函数</param>
    public static async Task UpdateConfigAsync(Action<AppConfig> updateAction)
    {
        lock (_lock)
        {
            updateAction?.Invoke(_currentConfig);
        }

        await SaveConfigAsync();
    }

    /// <summary>
    ///     重置为默认配置
    /// </summary>
    public static void ResetToDefault()
    {
        lock (_lock)
        {
            _currentConfig = CreateDefaultConfig();
            SaveConfig();
        }
    }

    /// <summary>
    ///     创建默认配置
    /// </summary>
    private static AppConfig CreateDefaultConfig()
    {
        return new AppConfig
        {
            PrivacyAgreed = false,
            IsTelemetryEnabled = false,
            // 遥测关闭时不生成 installation_id；仅在用户启用遥测后的首次上报时生成并覆盖此值。
            TelemetryInstallationId = string.Empty,
            Skin = Environment.OSVersion.Version.Build >= 22000
                ? "Mica"
                : OperatingSystem.IsMacOS()
                    ? "Acrylic"
                    : "None",
            KickWithoutDisable = true,
            HideInsteadOfClose = true,
            ParallelDownload = true,
            ParallelCount = 16,
            AutoStartup = false,
            AutoLaunch = false,
            AutoLaunchProxies =
            [
            ],
            ExpireDays = 30,
            DoNotShowSuccessMsg = true,
            Theme = "System",
            AccentColor = string.Empty,
            CaptchaMode = "implicit",
            DownloadSource = "TPCA",
            UpdateSettings = new UpdateSettings
            {
                AutoCheck = true,
                Channel = "Preview",
                Method = "ds",
                DownloadSource = Services.GitHubUpdateSources.Tpca,
                KeepOldVersion = UpdateSettings.UpdateOldVersionValues.Ask,
                NotifyRedundantVersion = true
            },
            HomeSettings = new HomeConfig
            {
                Layout = "classic",
                ShowStatistics = true,
                ShowUserInfo = true,
                ShowSystemInfo = true,
                ShowSystemNotice = true,
                ShowSoftwareNotice = true
            },
            BackgroundSettings = new BackgroundSettings
            {
                LayerOpacity = 0.6,
                BackgroundImage = string.Empty,
                // 注意：Stretch/TileMode 为 null 会让依赖 ToUpper(0) 的代码（如设置页）抛 NRE。
                // 默认使用与「未设置」行为一致的非空值（Stretch.None / disabled）。
                TileMode = "disabled",
                Stretch = "None",
                ShouldFillTitleBar = false
            },
            PMSettings = new PFSConfig
            {
                Position = "rt",
                Enabled = false
            },
            TerminalCli = "powershell",
            AutoLogin = false,
            AutoSign = false,
            Language = "zh-CN",
            TerminalEngineType = "Default",
            CreateProxyDefaults = new CreateProxyDefaults
            {
                LocalAddress = "127.0.0.1"
            },
            ProxyTemplates =
            [
            ],
            AnimationLevel = 2,
            SplashEnabled = true,
            SplashStyle = "default",
            SplashCustomImagePath = string.Empty,
        };
    }
}

public class AppConfig
{
    /// <summary>
    ///     配置文件 schema 版本。
    ///     <para>
    ///         用途：启动时校验配置是否为「最新 schema」。与 <see cref="ConfigManager.CurrentSchemaVersion" />
    ///         不一致时会自动补齐缺失字段、剔除已废弃字段、并把非法取值重置为默认值（不会丢失有效设置）。
    ///     </para>
    ///     <para>
    ///         语义与版本号无关：凡是「新增配置项 / 改变字段含义 / 客户端已不再使用某字段」的改动，
    ///         都应把 <see cref="ConfigManager.CurrentSchemaVersion" /> 递增 1。
    ///     </para>
    ///     <para>
    ///         旧配置没有该字段时反序列化得到 0，同样会触发一次归一化，可安全用来自愈历史遗留的脏数据。
    ///     </para>
    /// </summary>
    public int SchemaVersion
    {
        get;
        set;
    }

    public bool PrivacyAgreed
    {
        get;
        set;
    }

    public bool IsTelemetryEnabled
    {
        get;
        set;
    }

    /// <summary>
    ///     Cloudflare 匿名使用统计的随机安装标识（26.5.0）。
    ///     <para>
    ///         首次启用遥测时由 <c>TelemetryService</c> 生成随机 UUID 并持久化，同一安装长期复用。
    ///         禁止由用户名、邮箱、计算机名、MAC 地址、硬盘序列号、Windows SID、IP 地址或上述信息的哈希推导。
    ///     </para>
    ///     <para>用户清除或损坏本地配置时会重新生成（老配置本字段为空，属预期）。</para>
    /// </summary>
    public string TelemetryInstallationId
    {
        get;
        set;
    }

    public string Skin
    {
        get;
        set;
    }

    public bool KickWithoutDisable
    {
        get;
        set;
    }

    public bool HideInsteadOfClose
    {
        get;
        set;
    }

    public bool ParallelDownload
    {
        get;
        set;
    }

    public int ParallelCount
    {
        get;
        set;
    }

    public bool AutoStartup
    {
        get;
        set;
    }

    public bool AutoLaunch
    {
        get;
        set;
    }

    public List<ALPConfig> AutoLaunchProxies
    {
        get;
        set;
    }

    public int ExpireDays
    {
        get;
        set;
    }

    public bool DoNotShowSuccessMsg
    {
        get;
        set;
    }

    public string Theme
    {
        get;
        set;
    } = "Dark";

    public string AccentColor
    {
        get;
        set;
    }

    /// <summary>
    ///     <c>implicit</c>隐式验证<p />
    ///     <c>Explicit</c>显式验证
    /// </summary>
    public string CaptchaMode
    {
        get;
        set;
    }

    public string DownloadSource
    {
        get;
        set;
    }

    public UpdateSettings UpdateSettings
    {
        get;
        set;
    }

    public BackgroundSettings BackgroundSettings
    {
        get;
        set;
    }

    public HomeConfig HomeSettings
    {
        get;
        set;
    }

    public PFSConfig PMSettings
    {
        get;
        set;
    }

    /// <summary>创建隧道表单默认值</summary>
    public CreateProxyDefaults CreateProxyDefaults
    {
        get;
        set;
    }

    /// <summary>创建隧道模板列表（重启后仍在）</summary>
    public List<ProxyTemplate> ProxyTemplates
    {
        get;
        set;
    }

    public string TerminalEngineType
    {
        get;
        set;
    }

    public string TerminalCli
    {
        get;
        set;
    }

    public bool AutoLogin
    {
        get;
        set;
    }

    public bool AutoSign
    {
        get;
        set;
    }

    public string Language
    {
        get;
        set;
    }

    /// <summary>
    ///     动画程度: 0=关闭动画 1=精简 2=标准
    /// </summary>
    public int AnimationLevel
    {
        get;
        set;
    } = 2;

    /// <summary>是否显示启动画面（Splash，26.3.1 M2）</summary>
    public bool SplashEnabled
    {
        get;
        set;
    } = true;

    /// <summary>
    ///     Splash 样式：<c>default</c> 默认 / <c>dark</c> 深色 / <c>minimal</c> 简约（26.3.1 M2）
    /// </summary>
    public string SplashStyle
    {
        get;
        set;
    } = "default";

    /// <summary>自定义 Splash 背景图路径（可选；非空且文件存在时优先于 SplashStyle）</summary>
    public string SplashCustomImagePath
    {
        get;
        set;
    } = string.Empty;
}

public class HomeConfig
{
    /// <summary>
    ///     主页布局：<c>classic</c> 为传统完整主页（由各 <c>Show*</c> 开关控制），
    ///     <c>simple</c> 为精简主页（用户与额度 / 系统状态 / 为你推荐）。
    ///     旧配置无此字段时按经典布局处理，保证升级后界面不变。
    /// </summary>
    public string Layout
    {
        get;
        set;
    } = "classic";

    public bool ShowStatistics
    {
        get;
        set;
    }

    public bool ShowUserInfo
    {
        get;
        set;
    }

    public bool ShowSystemInfo
    {
        get;
        set;
    }

    public bool ShowSystemNotice
    {
        get;
        set;
    }

    public bool ShowSoftwareNotice
    {
        get;
        set;
    }
}

public class UpdateSettings
{
    /// <summary>
    ///     <see cref="UpdateSettings.KeepOldVersion" /> 的全部合法取值。
    ///     <para>
    ///         用「常量持有类」而非 enum：这些值要作为字符串直接落进
    ///     <c>Settings.json</c>，并在配置归一化里与历史值比对，用字符串可避免
    ///     枚举成员改名导致旧配置集体失效。
    ///     </para>
    /// </summary>
    public class UpdateOldVersionValues
    {
        /// <summary>每次升级前询问用户（默认）。</summary>
        public const string Ask = "Ask";

        /// <summary>保留旧版本目录作为回滚点。</summary>
        public const string Keep = "Keep";

        /// <summary>安装完成后删除全部历史版本目录。</summary>
        public const string Discard = "Discard";

        /// <summary>全部合法取值；顺序即 UI 下拉框顺序。</summary>
        public static readonly string[] All = [Ask, Keep, Discard];
    }

    public bool AutoCheck
    {
        get;
        set;
    }

    public string Channel
    {
        get;
        set;
    }

    /// <summary>
    ///     <p><c>ds</c> Directly Silent - 下载后直接安装</p>
    ///     <p><c>dd</c> Directly Download - 直接下载, 手动安装</p>
    ///     <c>md</c> Manual Download - 手动下载并安装
    /// </summary>
    public string Method
    {
        get;
        set;
    }

    /// <summary>
    ///     更新页下载源（26.4）。
    ///     <para>
    ///         <c>TPCA</c>：自建 Alist CDN（默认）；
    ///         其余为 GitHub 线路（<c>GitHub</c> / <c>GitHubGhProxy</c> / <c>GitHubMoeyy</c>），
    ///         通过 GitHub REST API 获取 Release 资产列表后下载。
    ///     </para>
    /// </summary>
    public string DownloadSource
    {
        get;
        set;
    } = Services.GitHubUpdateSources.Tpca;

    /// <summary>
    ///     升级后如何处理旧版本目录（26.5.0，分离式布局）。
    ///     <para>
    ///         合法取值：<c>Ask</c>（每次升级前询问，默认）/ <c>Keep</c>（保留回滚点）/
    ///         <c>Discard</c>（安装后删除全部历史版本目录）。
    ///     </para>
    ///     <para>
    ///         取值语义直接对应安装器的 <c>/nocleanup</c> 与 <c>/cleanup</c> 开关；
    ///         非法值由 <see cref="ConfigManager.NormalizeToLatestSchema" /> 归一化。
    ///     </para>
    /// </summary>
    public string KeepOldVersion
    {
        get;
        set;
    } = UpdateOldVersionValues.Ask;

    /// <summary>
    ///     是否在启动时检测到多余版本目录就提醒用户清理（26.5.0）。
    ///     <para>
    ///         仅对版本化布局有意义；每 7 天最多提醒一次，避免反复打扰。
    ///     </para>
    /// </summary>
    public bool NotifyRedundantVersion
    {
        get;
        set;
    } = true;
}

public class BackgroundSettings
{
    public double LayerOpacity
    {
        get;
        set;
    } = 0.5;

    public string BackgroundImage
    {
        get;
        set;
    } = "disabled";

    public string TileMode
    {
        get;
        set;
    } = "disabled";

    public string Stretch
    {
        get;
        set;
    } = "disabled";

    public bool ShouldFillTitleBar
    {
        get;
        set;
    }
}

public class PFSConfig
{
    public string Position
    {
        get;
        set;
    } = "rt";

    public bool Enabled
    {
        get;
        set;
    }

    /// <summary>
    ///     窗口穿透（默认关闭：悬浮窗正常接收鼠标）
    /// </summary>
    public bool ClickThrough
    {
        get;
        set;
    }

    /// <summary>
    ///     显示实时流量折线图（默认开启）
    /// </summary>
    public bool ShowChart
    {
        get;
        set;
    } = true;

    /// <summary>
    ///     悬浮窗不透明度（0.5–1.0）
    /// </summary>
    public double Opacity
    {
        get;
        set;
    } = 0.92;
}

public class ALPConfig
{
    public string Name
    {
        get;
        set;
    }

    public int Id
    {
        get;
        set;
    }

    public bool UseConfig
    {
        get;
        set;
    }

    public string Config
    {
        get;
        set;
    }
}