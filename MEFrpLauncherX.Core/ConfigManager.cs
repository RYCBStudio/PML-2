using Newtonsoft.Json;

namespace MEFrpLauncherX.Core
{
    public static class ConfigManager
    {
        private static readonly string ConfigDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
        public static string ConfigPath
        {
            get;
        } = Path.Combine(ConfigDirectory, "Settings.json");

        private static AppConfig _currentConfig;
        private static readonly object _lock = new();

        /// <summary>
        /// 初始化配置管理器
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
        /// 获取当前配置（只读）
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
        /// 加载配置文件
        /// </summary>
        public static void LoadConfig()
        {
            try
            {
                lock (_lock)
                {
                    var updateBakFile = ConfigPath + ".bak.update";
                    if (File.Exists(updateBakFile) &&
                        DateTime.Now.Subtract(File.GetLastWriteTime(ConfigPath)).TotalHours <= 1)
                    {
                        var _bak_json = File.ReadAllText(updateBakFile);
                        var _bak_config = JsonConvert.DeserializeObject<AppConfig>(_bak_json);

                        MergeConfig(_bak_config, _currentConfig);

                        try
                        {
                            File.Delete(updateBakFile);
                        }
                        catch
                        {
                        }
                    }

                    var json = File.ReadAllText(ConfigPath);
                    _currentConfig = JsonConvert.DeserializeObject<AppConfig>(json);
                }
            }
            catch (Exception ex)
            {
                // 如果加载失败，使用默认配置
                _currentConfig = CreateDefaultConfig();
                App.CurrentLogger?.Log($"加载配置文件失败，使用默认配置: {ex.Message}",
                    module: EnumLogModule.Custom, customModuleName: "配置管理");
            }
        }


        private static void MergeConfig(AppConfig source, AppConfig target)
        {
            if (source == null || target == null) return;

            if (!source.PrivacyAgreed && target.PrivacyAgreed)
                source.PrivacyAgreed = target.PrivacyAgreed;

            if (!source.IsTelemetryEnabled && target.IsTelemetryEnabled)
                source.IsTelemetryEnabled = target.IsTelemetryEnabled;

            if (string.IsNullOrEmpty(source.Skin) && !string.IsNullOrEmpty(target.Skin))
                source.Skin = target.Skin;

            if (!source.KickWithoutDisable && target.KickWithoutDisable)
                source.KickWithoutDisable = target.KickWithoutDisable;

            if (!source.HideInsteadOfClose && target.HideInsteadOfClose)
                source.HideInsteadOfClose = target.HideInsteadOfClose;

            if (!source.ParallelDownload && target.ParallelDownload)
                source.ParallelDownload = target.ParallelDownload;

            if (source.ParallelCount == 0 && target.ParallelCount != 0)
                source.ParallelCount = target.ParallelCount;

            if (!source.AutoStartup && target.AutoStartup)
                source.AutoStartup = target.AutoStartup;

            if (!source.AutoLaunch && target.AutoLaunch)
                source.AutoLaunch = target.AutoLaunch;

            if (source.AutoLaunchProxies != null && target.AutoLaunchProxies != null &&
                target.AutoLaunchProxies.Count > 0 && source.AutoLaunchProxies.Count == 0)
                source.AutoLaunchProxies = target.AutoLaunchProxies;

            if (source.ExpireDays == 0 && target.ExpireDays != 0)
                source.ExpireDays = target.ExpireDays;

            if (!source.DoNotShowSuccessMsg && target.DoNotShowSuccessMsg)
                source.DoNotShowSuccessMsg = target.DoNotShowSuccessMsg;

            if (string.IsNullOrEmpty(source.Theme) && !string.IsNullOrEmpty(target.Theme))
                source.Theme = target.Theme;

            if (string.IsNullOrEmpty(source.AccentColor) && !string.IsNullOrEmpty(target.AccentColor))
                source.AccentColor = target.AccentColor;

            if (string.IsNullOrEmpty(source.CaptchaMode) && !string.IsNullOrEmpty(target.CaptchaMode))
                source.CaptchaMode = target.CaptchaMode;

            if (string.IsNullOrEmpty(source.DownloadSource) && !string.IsNullOrEmpty(target.DownloadSource))
                source.DownloadSource = target.DownloadSource;

            if (source.UpdateSettings == null && target.UpdateSettings != null)
                source.UpdateSettings = target.UpdateSettings;
            else if (source.UpdateSettings != null && target.UpdateSettings != null)
                MergeUpdateSettings(source.UpdateSettings, target.UpdateSettings);

            if (source.BackgroundSettings == null && target.BackgroundSettings != null)
                source.BackgroundSettings = target.BackgroundSettings;
            else if (source.BackgroundSettings != null && target.BackgroundSettings != null)
                MergeBackgroundSettings(source.BackgroundSettings, target.BackgroundSettings);

            if (source.PMSettings == null && target.PMSettings != null)
                source.PMSettings = target.PMSettings;
        }

        private static void MergeUpdateSettings(UpdateSettings source, UpdateSettings target)
        {
            if (!source.AutoCheck && target.AutoCheck)
                source.AutoCheck = target.AutoCheck;

            if (source.Method != target.Method)
                source.Method = target.Method;

            if (source.Channel != target.Channel)
                source.Channel = target.Channel;
        }

        private static void MergeBackgroundSettings(BackgroundSettings source, BackgroundSettings target)
        {
            if (string.IsNullOrEmpty(source.BackgroundImage) && !string.IsNullOrEmpty(target.BackgroundImage))
                source.BackgroundImage = target.BackgroundImage;

            if (string.IsNullOrEmpty(source.Stretch) && !string.IsNullOrEmpty(target.Stretch))
                source.Stretch = target.Stretch;

            if (string.IsNullOrEmpty(source.TileMode) && !string.IsNullOrEmpty(target.TileMode))
                source.TileMode = target.TileMode;

            if (source.LayerOpacity == 0 && target.LayerOpacity != 0)
                source.LayerOpacity = target.LayerOpacity;
        }

        /// <summary>
        /// 保存当前配置到文件
        /// </summary>
        public static void SaveConfig()
        {
            try
            {
                lock (_lock)
                {
                    var json = JsonConvert.SerializeObject(_currentConfig, Formatting.Indented);
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
        /// 异步保存当前配置到文件
        /// </summary>
        public static async Task SaveConfigAsync()
        {
            try
            {
                string json;
                lock (_lock)
                {
                    json = JsonConvert.SerializeObject(_currentConfig, Formatting.Indented);
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
        /// 更新配置并保存
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
        /// 异步更新配置并保存
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
        /// 重置为默认配置
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
        /// 创建默认配置
        /// </summary>
        private static AppConfig CreateDefaultConfig()
        {
            return new AppConfig
            {
                PrivacyAgreed = false,
                IsTelemetryEnabled = false,
                Skin = "None",
                KickWithoutDisable = true,
                HideInsteadOfClose = true,
                ParallelDownload = true,
                ParallelCount = 16,
                AutoStartup = false,
                AutoLaunch = false,
                AutoLaunchProxies = [],
                ExpireDays = 30,
                DoNotShowSuccessMsg = true,
                Theme = "Dark",
                AccentColor = null,
                CaptchaMode = "implicit",
                DownloadSource = "TPCA",
                UpdateSettings = new UpdateSettings
                {
                    AutoCheck = true,
                    Channel = "Preview",
                    Method = "ds",
                    KeepProfile = true
                },
                BackgroundSettings = new BackgroundSettings
                {
                    LayerOpacity = 0.5,
                    BackgroundImage = null,
                    TileMode = null,
                    Stretch = null
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
        /// <c>implicit</c>隐式验证<p/>
        /// <c>Explicit</c>显式验证
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


        public PFSConfig PMSettings
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
        /// <p><c>ds</c> Directly Silent - 下载后直接安装</p>
        /// <p><c>dd</c> Directly Download - 直接下载, 手动安装</p>
        /// <c>md</c> Manual Download - 手动下载并安装
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
}