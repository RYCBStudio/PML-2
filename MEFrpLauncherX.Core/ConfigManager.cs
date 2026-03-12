using Newtonsoft.Json;

namespace MEFrpLauncherX.Core
{
    public static class ConfigManager
    {
        private static readonly string ConfigDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
        private static readonly string ConfigPath = Path.Combine(ConfigDirectory, "Settings.json");

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
                Skin = "Mica",
                KickWithoutDisable = false,
                HideInsteadOfClose = true,
                ParallelDownload = true,
                ParallelCount = 8,
                AutoStartup = false,
                AutoLaunch = false,
                AutoLaunchProxies = [],
                Theme = "Dark",
                ExpireDays = 7
            };
        }
    }

    public class AppConfig
    {
        
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
            init;
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