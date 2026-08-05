using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MEFrpLauncherX.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MEFrpLauncherX.Core.MEFIntergrated;

public class FrpConfigService
{
    private readonly JsonSerializerOptions _jsonSettings = new()
    {
        WriteIndented = true
    };

    private readonly IDeserializer _yamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private readonly ISerializer _yamlSerializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    // 添加 HTTPS 代理的便捷方法（保持不变）
    public void AddHttpsProxy(FrpConfig config, string name, string domain,
        string localAddr, string crtPath, string keyPath)
    {
        var proxy = new ProxyConfig
        {
            Name = name,
            Type = "https",
            CustomDomains = [domain],
            Plugin = new PluginConfig
            {
                Type = "https2http",
                LocalAddr = localAddr,
                CrtPath = crtPath,
                KeyPath = keyPath
            },
            Transport = new TransportConfig
            {
                UseEncryption = true,
                UseCompression = true
            }
        };

        config.Proxies.Add(proxy);
    }

    #region 加载配置 - 多格式支持

    public FrpConfig LoadConfig(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLower();
        var content = File.ReadAllText(filePath);

        return extension switch
        {
            //".toml" => LoadConfigFromToml(content),
            ".json" => LoadConfigFromJson(content),
            ".yaml" or ".yml" => LoadConfigFromYaml(content),
            ".ini" => LoadConfigFromIni(content),
            _ => throw new NotSupportedException($"不支持的配置文件格式: {extension}")
        };
    }

    // public FrpConfig LoadConfigFromToml(string tomlContent)
    // {
    //     var model = TomlReader.Create()
    //     var config = new FrpConfig();
    //
    //     // 解析基础配置
    //     config.ServerAddr = model["serverAddr"]?.ToString() ?? string.Empty;
    //     config.ServerPort = Convert.ToInt32(model["serverPort"]);
    //     config.User = model["user"]?.ToString() ?? string.Empty;
    //
    //     // 解析认证配置
    //     if (model["auth"] is TomlTable authTable)
    //     {
    //         config.Auth.Method = authTable["method"]?.ToString() ?? "token";
    //         config.Auth.Token = authTable["token"]?.ToString() ?? string.Empty;
    //     }
    //
    //     // 解析代理配置
    //     if (model["proxies"] is TomlTableArray proxiesArray)
    //     {
    //         foreach (var proxyTable in proxiesArray)
    //         {
    //             var proxy = new ProxyConfig
    //             {
    //                 Name = proxyTable["name"]?.ToString() ?? string.Empty,
    //                 Type = proxyTable["type"]?.ToString() ?? "tcp",
    //                 LocalIP = proxyTable["localIP"]?.ToString() ?? "127.0.0.1",
    //                 LocalPort = Convert.ToInt32(proxyTable["localPort"]),
    //                 RemotePort = Convert.ToInt32(proxyTable["remotePort"])
    //             };
    //
    //             // 解析自定义域名（HTTPS 代理需要）
    //             if (proxyTable["customDomains"] is TomlArray domainsArray)
    //             {
    //                 proxy.CustomDomains = domainsArray.Select(d => d.ToString()).ToList();
    //             }
    //
    //             // 解析插件配置
    //             if (proxyTable["plugin"] is TomlTable pluginTable)
    //             {
    //                 proxy.Plugin = new PluginConfig
    //                 {
    //                     Type = pluginTable["type"]?.ToString() ?? string.Empty,
    //                     LocalAddr = pluginTable["localAddr"]?.ToString() ?? string.Empty,
    //                     CrtPath = pluginTable["crtPath"]?.ToString() ?? string.Empty,
    //                     KeyPath = pluginTable["keyPath"]?.ToString() ?? string.Empty
    //                 };
    //             }
    //
    //             // 解析传输配置
    //             if (proxyTable["transport"] is TomlTable transportTable)
    //             {
    //                 proxy.Transport = new TransportConfig
    //                 {
    //                     UseEncryption = Convert.ToBoolean(transportTable["useEncryption"]),
    //                     UseCompression = Convert.ToBoolean(transportTable["useCompression"])
    //                 };
    //             }
    //
    //             config.Proxies.Add(proxy);
    //         }
    //     }
    //
    //     return config;
    // }

    public FrpConfig LoadConfigFromJson(string jsonContent)
    {
        var jsonModel = JsonSerializer.Deserialize<JsonFrpConfig>(jsonContent, _jsonSettings);
        return ConvertFromJsonModel(jsonModel);
    }

    public FrpConfig LoadConfigFromYaml(string yamlContent)
    {
        var yamlModel = _yamlDeserializer.Deserialize<YamlFrpConfig>(yamlContent);
        return ConvertFromYamlModel(yamlModel);
    }

    public FrpConfig LoadConfigFromIni(string iniContent)
    {
        var iniModel = ParseIniContent(iniContent);
        return ConvertFromIniModel(iniModel);
    }

    #endregion

    #region 保存配置 - 多格式支持

    public string SaveConfig(FrpConfig config, string format = "toml")
    {
        return format.ToLower() switch
        {
            //"toml" => SaveAsToml(config),
            "json" => SaveAsJson(config),
            "yaml" => SaveAsYaml(config),
            "ini" => SaveAsIni(config)
            // _ => SaveAsToml(config)
        };
    }

    // private string SaveAsToml(FrpConfig config)
    // {
    //     var table = new TomlTable
    //     {
    //         ["serverAddr"] = config.ServerAddr,
    //         ["serverPort"] = config.ServerPort,
    //         ["user"] = config.User
    //     };
    //
    //     // 认证配置
    //     var authTable = new TomlTable
    //     {
    //         ["method"] = config.Auth.Method,
    //         ["token"] = config.Auth.Token
    //     };
    //     table.Add("auth", authTable);
    //
    //     // 代理配置
    //     var proxiesArray = new TomlTableArray();
    //     foreach (var proxy in config.Proxies)
    //     {
    //         var proxyTable = new TomlTable
    //         {
    //             ["name"] = proxy.Name,
    //             ["type"] = proxy.Type,
    //             ["localIP"] = proxy.LocalIP,
    //             ["localPort"] = proxy.LocalPort,
    //             ["remotePort"] = proxy.RemotePort
    //         };
    //
    //         // 自定义域名
    //         if (proxy.CustomDomains.Any())
    //         {
    //             proxyTable["customDomains"] = new TomlArray
    //             {
    //                 proxy.CustomDomains.Select(object (d) => d).ToList()
    //             };
    //         }
    //
    //         // 插件配置
    //         if (!string.IsNullOrEmpty(proxy.Plugin.Type))
    //         {
    //             var pluginTable = new TomlTable
    //             {
    //                 ["type"] = proxy.Plugin.Type,
    //                 ["localAddr"] = proxy.Plugin.LocalAddr,
    //                 ["crtPath"] = proxy.Plugin.CrtPath,
    //                 ["keyPath"] = proxy.Plugin.KeyPath
    //             };
    //             proxyTable.Add("plugin", pluginTable);
    //         }
    //
    //         // 传输配置
    //         var transportTable = new TomlTable
    //         {
    //             ["useEncryption"] = proxy.Transport.UseEncryption,
    //             ["useCompression"] = proxy.Transport.UseCompression
    //         };
    //         proxyTable.Add("transport", transportTable);
    //
    //         proxiesArray.Add(proxyTable);
    //     }
    //
    //     table.Add("proxies", proxiesArray);
    //
    //     return Toml.FromModel(table);
    // }


    private string SaveAsJson(FrpConfig config)
    {
        var jsonModel = ConvertToJsonModel(config);
        return JsonSerializer.Serialize(jsonModel, _jsonSettings);
    }

    private string SaveAsYaml(FrpConfig config)
    {
        var yamlModel = ConvertToYamlModel(config);
        return _yamlSerializer.Serialize(yamlModel);
    }

    private string SaveAsIni(FrpConfig config) => GenerateIniContent(config);

    #endregion

    #region JSON 格式处理

    // JSON 数据模型
    public class JsonFrpConfig
    {
        [JsonPropertyName("serverAddr")]
        public string ServerAddr
        {
            get;
            set;
        } = string.Empty;

        [JsonPropertyName("serverPort")]
        public int ServerPort
        {
            get;
            set;
        }

        [JsonPropertyName("user")]
        public string User
        {
            get;
            set;
        } = string.Empty;

        [JsonPropertyName("auth")]
        public JsonAuthConfig Auth
        {
            get;
            set;
        } = new();

        [JsonPropertyName("proxies")]
        public List<JsonProxyConfig> Proxies
        {
            get;
            set;
        } = [];
    }

    public class JsonAuthConfig
    {
        [JsonPropertyName("method")]
        public string Method
        {
            get;
            set;
        } = "token";

        [JsonPropertyName("token")]
        public string Token
        {
            get;
            set;
        } = string.Empty;
    }

    public class JsonProxyConfig
    {
        [JsonPropertyName("name")]
        public string Name
        {
            get;
            set;
        } = string.Empty;

        [JsonPropertyName("type")]
        public string Type
        {
            get;
            set;
        } = "tcp";

        [JsonPropertyName("localIP")]
        public string LocalIP
        {
            get;
            set;
        } = "127.0.0.1";

        [JsonPropertyName("localPort")]
        public int LocalPort
        {
            get;
            set;
        }

        [JsonPropertyName("remotePort")]
        public int RemotePort
        {
            get;
            set;
        }

        [JsonPropertyName("customDomains")]
        public List<string> CustomDomains
        {
            get;
            set;
        } = [];

        [JsonPropertyName("plugin")]
        public JsonPluginConfig Plugin
        {
            get;
            set;
        } = new();

        [JsonPropertyName("transport")]
        public JsonTransportConfig Transport
        {
            get;
            set;
        } = new();
    }

    public class JsonPluginConfig
    {
        [JsonPropertyName("type")]
        public string Type
        {
            get;
            set;
        } = string.Empty;

        [JsonPropertyName("localAddr")]
        public string LocalAddr
        {
            get;
            set;
        } = string.Empty;

        [JsonPropertyName("crtPath")]
        public string CrtPath
        {
            get;
            set;
        } = string.Empty;

        [JsonPropertyName("keyPath")]
        public string KeyPath
        {
            get;
            set;
        } = string.Empty;
    }

    public class JsonTransportConfig
    {
        [JsonPropertyName("useEncryption")]
        public bool UseEncryption
        {
            get;
            set;
        } = true;

        [JsonPropertyName("useCompression")]
        public bool UseCompression
        {
            get;
            set;
        } = true;
    }

    private JsonFrpConfig ConvertToJsonModel(FrpConfig config)
    {
        return new JsonFrpConfig
        {
            ServerAddr = config.ServerAddr,
            ServerPort = config.ServerPort,
            User = config.User,
            Auth = new JsonAuthConfig
            {
                Method = config.Auth.Method,
                Token = config.Auth.Token
            },
            Proxies = config.Proxies.Select(p => new JsonProxyConfig
            {
                Name = p.Name,
                Type = p.Type,
                LocalIP = p.LocalIP,
                LocalPort = p.LocalPort,
                RemotePort = p.RemotePort,
                CustomDomains = p.CustomDomains,
                Plugin = new JsonPluginConfig
                {
                    Type = p.Plugin.Type,
                    LocalAddr = p.Plugin.LocalAddr,
                    CrtPath = p.Plugin.CrtPath,
                    KeyPath = p.Plugin.KeyPath
                },
                Transport = new JsonTransportConfig
                {
                    UseEncryption = p.Transport.UseEncryption,
                    UseCompression = p.Transport.UseCompression
                }
            }).ToList()
        };
    }

    private FrpConfig ConvertFromJsonModel(JsonFrpConfig jsonModel)
    {
        if (jsonModel == null)
        {
            return new FrpConfig();
        }

        return new FrpConfig
        {
            ServerAddr = jsonModel.ServerAddr,
            ServerPort = jsonModel.ServerPort,
            User = jsonModel.User,
            Auth = new AuthConfig
            {
                Method = jsonModel.Auth.Method,
                Token = jsonModel.Auth.Token
            },
            Proxies = jsonModel.Proxies.Select(p => new ProxyConfig
            {
                Name = p.Name,
                Type = p.Type,
                LocalIP = p.LocalIP,
                LocalPort = p.LocalPort,
                RemotePort = p.RemotePort,
                CustomDomains = p.CustomDomains,
                Plugin = new PluginConfig
                {
                    Type = p.Plugin.Type,
                    LocalAddr = p.Plugin.LocalAddr,
                    CrtPath = p.Plugin.CrtPath,
                    KeyPath = p.Plugin.KeyPath
                },
                Transport = new TransportConfig
                {
                    UseEncryption = p.Transport.UseEncryption,
                    UseCompression = p.Transport.UseCompression
                }
            }).ToList()
        };
    }

    #endregion

    #region YAML 格式处理

    // YAML 数据模型
    public class YamlFrpConfig
    {
        [YamlMember(Alias = "serverAddr")]
        public string ServerAddr
        {
            get;
            set;
        } = string.Empty;

        [YamlMember(Alias = "serverPort")]
        public int ServerPort
        {
            get;
            set;
        }

        [YamlMember(Alias = "user")]
        public string User
        {
            get;
            set;
        } = string.Empty;

        [YamlMember(Alias = "auth")]
        public YamlAuthConfig Auth
        {
            get;
            set;
        } = new();

        [YamlMember(Alias = "proxies")]
        public List<YamlProxyConfig> Proxies
        {
            get;
            set;
        } = [];
    }

    public class YamlAuthConfig
    {
        [YamlMember(Alias = "method")]
        public string Method
        {
            get;
            set;
        } = "token";

        [YamlMember(Alias = "token")]
        public string Token
        {
            get;
            set;
        } = string.Empty;
    }

    public class YamlProxyConfig
    {
        [YamlMember(Alias = "name")]
        public string Name
        {
            get;
            set;
        } = string.Empty;

        [YamlMember(Alias = "type")]
        public string Type
        {
            get;
            set;
        } = "tcp";

        [YamlMember(Alias = "localIP")]
        public string LocalIP
        {
            get;
            set;
        } = "127.0.0.1";

        [YamlMember(Alias = "localPort")]
        public int LocalPort
        {
            get;
            set;
        }

        [YamlMember(Alias = "remotePort")]
        public int RemotePort
        {
            get;
            set;
        }

        [YamlMember(Alias = "customDomains")]
        public List<string> CustomDomains
        {
            get;
            set;
        } = [];

        [YamlMember(Alias = "plugin")]
        public YamlPluginConfig Plugin
        {
            get;
            set;
        } = new();

        [YamlMember(Alias = "transport")]
        public YamlTransportConfig Transport
        {
            get;
            set;
        } = new();
    }

    public class YamlPluginConfig
    {
        [YamlMember(Alias = "type")]
        public string Type
        {
            get;
            set;
        } = string.Empty;

        [YamlMember(Alias = "localAddr")]
        public string LocalAddr
        {
            get;
            set;
        } = string.Empty;

        [YamlMember(Alias = "crtPath")]
        public string CrtPath
        {
            get;
            set;
        } = string.Empty;

        [YamlMember(Alias = "keyPath")]
        public string KeyPath
        {
            get;
            set;
        } = string.Empty;
    }

    public class YamlTransportConfig
    {
        [YamlMember(Alias = "useEncryption")]
        public bool UseEncryption
        {
            get;
            set;
        } = true;

        [YamlMember(Alias = "useCompression")]
        public bool UseCompression
        {
            get;
            set;
        } = true;
    }

    private YamlFrpConfig ConvertToYamlModel(FrpConfig config)
    {
        return new YamlFrpConfig
        {
            ServerAddr = config.ServerAddr,
            ServerPort = config.ServerPort,
            User = config.User,
            Auth = new YamlAuthConfig
            {
                Method = config.Auth.Method,
                Token = config.Auth.Token
            },
            Proxies = config.Proxies.Select(p => new YamlProxyConfig
            {
                Name = p.Name,
                Type = p.Type,
                LocalIP = p.LocalIP,
                LocalPort = p.LocalPort,
                RemotePort = p.RemotePort,
                CustomDomains = p.CustomDomains,
                Plugin = new YamlPluginConfig
                {
                    Type = p.Plugin.Type,
                    LocalAddr = p.Plugin.LocalAddr,
                    CrtPath = p.Plugin.CrtPath,
                    KeyPath = p.Plugin.KeyPath
                },
                Transport = new YamlTransportConfig
                {
                    UseEncryption = p.Transport.UseEncryption,
                    UseCompression = p.Transport.UseCompression
                }
            }).ToList()
        };
    }

    private FrpConfig ConvertFromYamlModel(YamlFrpConfig yamlModel)
    {
        if (yamlModel == null)
        {
            return new FrpConfig();
        }

        return new FrpConfig
        {
            ServerAddr = yamlModel.ServerAddr,
            ServerPort = yamlModel.ServerPort,
            User = yamlModel.User,
            Auth = new AuthConfig
            {
                Method = yamlModel.Auth.Method,
                Token = yamlModel.Auth.Token
            },
            Proxies = yamlModel.Proxies.Select(p => new ProxyConfig
            {
                Name = p.Name,
                Type = p.Type,
                LocalIP = p.LocalIP,
                LocalPort = p.LocalPort,
                RemotePort = p.RemotePort,
                CustomDomains = p.CustomDomains,
                Plugin = new PluginConfig
                {
                    Type = p.Plugin.Type,
                    LocalAddr = p.Plugin.LocalAddr,
                    CrtPath = p.Plugin.CrtPath,
                    KeyPath = p.Plugin.KeyPath
                },
                Transport = new TransportConfig
                {
                    UseEncryption = p.Transport.UseEncryption,
                    UseCompression = p.Transport.UseCompression
                }
            }).ToList()
        };
    }

    #endregion

    #region INI 格式处理

    private string GenerateIniContent(FrpConfig config)
    {
        var sb = new StringBuilder();

        // 基础配置
        sb.AppendLine("[common]");
        sb.AppendLine($"server_addr = {config.ServerAddr}");
        sb.AppendLine($"server_port = {config.ServerPort}");
        sb.AppendLine($"user = {config.User}");
        sb.AppendLine();

        // 认证配置
        sb.AppendLine("[auth]");
        sb.AppendLine($"method = {config.Auth.Method}");
        sb.AppendLine($"token = {config.Auth.Token}");
        sb.AppendLine();

        // 代理配置
        foreach (var proxy in config.Proxies)
        {
            sb.AppendLine($"[{proxy.Name}]");
            sb.AppendLine($"type = {proxy.Type}");
            sb.AppendLine($"local_ip = {proxy.LocalIP}");
            sb.AppendLine($"local_port = {proxy.LocalPort}");
            sb.AppendLine($"remote_port = {proxy.RemotePort}");

            // 自定义域名
            if (proxy.CustomDomains.Any())
            {
                sb.AppendLine($"custom_domains = {string.Join(",", proxy.CustomDomains)}");
            }

            // 插件配置
            if (!string.IsNullOrEmpty(proxy.Plugin.Type))
            {
                sb.AppendLine($"plugin = {proxy.Plugin.Type}");
                sb.AppendLine($"plugin_local_addr = {proxy.Plugin.LocalAddr}");
                sb.AppendLine($"plugin_crt_path = {proxy.Plugin.CrtPath}");
                sb.AppendLine($"plugin_key_path = {proxy.Plugin.KeyPath}");
            }

            // 传输配置
            sb.AppendLine($"use_encryption = {proxy.Transport.UseEncryption.ToString().ToLower()}");
            sb.AppendLine($"use_compression = {proxy.Transport.UseCompression.ToString().ToLower()}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private Dictionary<string, Dictionary<string, string>> ParseIniContent(string iniContent)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>();
        Dictionary<string, string> currentSection = null;
        var currentSectionName = "common";

        foreach (var line in iniContent.Split('\n'))
        {
            var trimmedLine = line.Trim();

            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(";") || trimmedLine.StartsWith("#"))
            {
                continue;
            }

            if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
            {
                currentSectionName = trimmedLine.Substring(1, trimmedLine.Length - 2);
                currentSection = new Dictionary<string, string>();
                sections[currentSectionName] = currentSection;
            }
            else if (currentSection != null && trimmedLine.Contains('='))
            {
                var parts = trimmedLine.Split('=', 2);
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim();
                    var value = parts[1].Trim();
                    currentSection[key] = value;
                }
            }
        }

        return sections;
    }

    private FrpConfig ConvertFromIniModel(Dictionary<string, Dictionary<string, string>> iniModel)
    {
        var config = new FrpConfig();

        // 解析 common 段
        if (iniModel.TryGetValue("common", out var commonSection))
        {
            config.ServerAddr = commonSection.GetValueOrDefault("server_addr", "");
            config.ServerPort = int.Parse(commonSection.GetValueOrDefault("server_port", "0"));
            config.User = commonSection.GetValueOrDefault("user", "");
        }

        // 解析 auth 段
        if (iniModel.TryGetValue("auth", out var authSection))
        {
            config.Auth.Method = authSection.GetValueOrDefault("method", "token");
            config.Auth.Token = authSection.GetValueOrDefault("token", "");
        }

        // 解析代理段
        foreach (var section in iniModel)
        {
            if (section.Key != "common" && section.Key != "auth")
            {
                var proxy = new ProxyConfig
                {
                    Name = section.Key,
                    Type = section.Value.GetValueOrDefault("type", "tcp"),
                    LocalIP = section.Value.GetValueOrDefault("local_ip", "127.0.0.1"),
                    LocalPort = int.Parse(section.Value.GetValueOrDefault("local_port", "0")),
                    RemotePort = int.Parse(section.Value.GetValueOrDefault("remote_port", "0")),
                    Plugin = new PluginConfig
                    {
                        Type = section.Value.GetValueOrDefault("plugin", ""),
                        LocalAddr = section.Value.GetValueOrDefault("plugin_local_addr", ""),
                        CrtPath = section.Value.GetValueOrDefault("plugin_crt_path", ""),
                        KeyPath = section.Value.GetValueOrDefault("plugin_key_path", "")
                    },
                    Transport = new TransportConfig
                    {
                        UseEncryption = bool.Parse(section.Value.GetValueOrDefault("use_encryption", "true")),
                        UseCompression = bool.Parse(section.Value.GetValueOrDefault("use_compression", "true"))
                    }
                };

                // 解析自定义域名
                if (section.Value.TryGetValue("custom_domains", out var domains))
                {
                    proxy.CustomDomains = domains.Split(',').Select(d => d.Trim()).ToList();
                }

                config.Proxies.Add(proxy);
            }
        }

        return config;
    }

    #endregion
}