using System.Text.Json;

// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
#pragma warning disable CS8625 // 无法将 null 字面量转换为非 null 的引用类型。
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 'required' 修饰符或声明为可以为 null。

namespace MEFrpLauncherX.Core;

public static class ConfigManager
{
    private static readonly string ConfigDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");

    private static AppConfig _currentConfig;
    private static readonly object _lock = new();

    public static string ConfigPath
    {
        get;
    } = Path.Combine(ConfigDirectory, "Settings.json");

    public static string BackupConfigPath
    {
        get;
    } = Path.Combine(ConfigDirectory, "Settings.json.bak.update");

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
            SaveConfig();
        }
        else
        {
            LoadConfig();
        }
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
                var updateBakFile = BackupConfigPath;
                if (!File.Exists(updateBakFile))
                {
                    return;
                }

                App.CurrentLogger?.Log($"正在合并更新配置文件: {updateBakFile}",
                    module: EnumLogModule.Custom, customModuleName: "配置管理");

                var _bak_json = File.ReadAllText(updateBakFile);
                var _bak_config =
                    JsonSerializer.Deserialize<AppConfig>(_bak_json, App.AppJsonSerializerContext.AppConfig);
                App.CurrentLogger?.Log($"正在合并更新配置文件: {updateBakFile}",
                    module: EnumLogModule.Custom, customModuleName: "配置管理");

                MergeConfig(_bak_config, ref _currentConfig);
                App.CurrentLogger?.Log($"合并更新配置文件: {updateBakFile} 完成",
                    module: EnumLogModule.Custom, customModuleName: "配置管理");

                try
                {
                    File.Delete(updateBakFile);
                    File.Delete(Path.Combine(ConfigDirectory, "KEEP_PROFILE"));
                }
                catch
                {
                    App.CurrentLogger?.Log($"删除更新配置文件失败: {updateBakFile}",
                        module: EnumLogModule.Custom, customModuleName: "配置管理");
                }
            }
        }
        catch (Exception ex)
        {
            // 如果加载失败，使用默认配置
            lock (_lock)
            {
                _currentConfig = CreateDefaultConfig();
            }

            App.CurrentLogger?.Log($"加载配置文件失败，使用默认配置: {ex.Message}",
                module: EnumLogModule.Custom, customModuleName: "配置管理");
        }
    }


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
        if (target.AutoLaunchProxies != null && source.AutoLaunchProxies != null &&
            source.AutoLaunchProxies.Count > 0 && target.AutoLaunchProxies.Count == 0)
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

        App.CurrentLogger?.Log($"正在合并配置项 Update>KeepProfile: {source.KeepProfile} -> {target.KeepProfile}",
            module: EnumLogModule.Custom, customModuleName: "配置管理");
        if (source.KeepProfile != target.KeepProfile)
        {
            source.KeepProfile = target.KeepProfile;
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
                var json = JsonSerializer.Serialize(_currentConfig, App.AppJsonSerializerContext.AppConfig);
                File.WriteAllText(ConfigPath, json);
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
                json = JsonSerializer.Serialize(_currentConfig, App.AppJsonSerializerContext.AppConfig);
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
            Skin = Environment.OSVersion.Version.Build >= 22000 ? "Mica" :
                OperatingSystem.IsMacOS() ? "Acrylic" : "None",
            KickWithoutDisable = true,
            HideInsteadOfClose = true,
            ParallelDownload = true,
            ParallelCount = 16,
            AutoStartup = false,
            AutoLaunch = false,
            AutoLaunchProxies = [],
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
                KeepProfile = true
            },
            HomeSettings = new HomeConfig
            {
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
                TileMode = null,
                Stretch = null,
                ShouldFillTitleBar = false
            },
            PMSettings = new PFSConfig
            {
                Position = "rt",
                Enabled = false
            }
        };
    }
}

public class AppConfig
{
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
}

public class HomeConfig
{
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

    public bool KeepProfile
    {
        get;
        set;
    }
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