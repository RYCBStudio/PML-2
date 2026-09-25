using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MEFrpLauncherX.Core.Models;

namespace MEFrpLauncherX.Core.Services;

/// <summary>
///     DNS 账户保险箱（26.4 阶段 B）：把 DNS 服务商凭据加密后保存在本机。
///     设计要点：
///     <list type="bullet">
///         <item>路径 <c>%AppData%/PML2/certs/dns_accounts.dat</c>，与安装目录分离，升级不会覆盖；</item>
///         <item>加密方式沿用项目既有本地密钥方案（随机 256 位密钥，Windows 下由 DPAPI 保护密钥文件），
///               与用户登录信息（<c>SecureStorage</c>）同思路但使用<b>独立密钥</b>；</item>
///         <item>文件内<b>不出现</b>任何明文 Token，凭据也不写入日志与遥测；</item>
///         <item>不依赖第三方库，AOT 安全（仅 <see cref="System.Text.Json" /> 源生成）。</item>
///     </list>
/// </summary>
public static class DnsAccountStore
{
    private const string KeyFileName = "dns_accounts.key";

    private static readonly object SyncRoot = new();

    private static byte[]? _cachedKey;

    /// <summary>
    ///     本存储专用的 JSON 上下文：<see cref="App.AppJsonSerializerContext" /> 仅在
    ///     <c>App.Init()</c> 之后可用，而账户读写可能在初始化前后都被调用，
    ///     因此这里自备一个源生成上下文（AOT 安全，不依赖反射）。
    /// </summary>
    private static AppJsonSerializerContext Json { get; } = new(new JsonSerializerOptions
    {
        WriteIndented = false
    });

    private static string StorageDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PML2",
        "certs");

    private static string KeyDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PML2",
        "keys");

    /// <summary>加密账户文件路径（供诊断与隐私说明展示）。</summary>
    public static string StorePath => Path.Combine(StorageDirectory, "dns_accounts.dat");

    /// <summary>列出全部账户摘要（<b>不含凭据</b>），按更新时间倒序。</summary>
    public static List<DnsAccountSummary> List()
    {
        lock (SyncRoot)
        {
            return
            [
                .. LoadFile()
                    .Accounts
                    .Select(a => new DnsAccountSummary
                    {
                        Id = a.Meta.Id,
                        DisplayName = a.Meta.DisplayName,
                        Provider = a.Meta.Provider,
                        ProviderDisplayName = DnsProviders.GetDisplayName(a.Meta.Provider),
                        UpdatedAt = a.Meta.UpdatedAt
                    })
                    .OrderByDescending(a => a.UpdatedAt)
            ];
        }
    }

    /// <summary>读取单个账户（含明文凭据），不存在时返回 null。</summary>
    public static DnsAccountEntry? Get(Guid id)
    {
        lock (SyncRoot)
        {
            return LoadFile().Accounts.FirstOrDefault(a => a.Meta.Id == id);
        }
    }

    /// <summary>
    ///     新增或更新账户。<paramref name="credentials" /> 中为空的字段会被剔除，
    ///     避免把空字符串写进 lego 的环境变量。
    /// </summary>
    /// <returns>成功返回 true；加密或落盘失败返回 false（原因已记日志）。</returns>
    public static bool Save(DnsAccount meta, IReadOnlyDictionary<string, string> credentials)
    {
        if (meta.Id == Guid.Empty)
        {
            meta.Id = Guid.NewGuid();
        }

        lock (SyncRoot)
        {
            try
            {
                var file = LoadFile();
                var existing = file.Accounts.FirstOrDefault(a => a.Meta.Id == meta.Id);
                var now = DateTimeOffset.UtcNow;

                var descriptor = DnsProviders.Resolve(meta.Provider);
                var cleaned = new Dictionary<string, string>();
                if (descriptor is not null)
                {
                    foreach (var field in descriptor.Fields)
                    {
                        if (!credentials.TryGetValue(field.Key, out var value))
                        {
                            continue;
                        }

                        value = value.Trim();
                        if (value.Length == 0)
                        {
                            continue;
                        }

                        cleaned[field.Key] = value;
                    }
                }

                if (existing is null)
                {
                    meta.CreatedAt = now;
                    meta.UpdatedAt = now;
                    file.Accounts.Add(new DnsAccountEntry { Meta = meta, Credentials = cleaned });
                }
                else
                {
                    meta.CreatedAt = existing.Meta.CreatedAt;
                    meta.UpdatedAt = now;
                    existing.Meta = meta;
                    existing.Credentials = cleaned;
                }

                return WriteFile(file);
            }
            catch (Exception ex)
            {
                App.CurrentLogger?.Error(ex, "保存 DNS 账户失败");
                return false;
            }
        }
    }

    /// <summary>删除账户；成功返回 true。</summary>
    public static bool Delete(Guid id)
    {
        lock (SyncRoot)
        {
            try
            {
                var file = LoadFile();
                var removed = file.Accounts.RemoveAll(a => a.Meta.Id == id);
                if (removed == 0)
                {
                    return false;
                }

                return WriteFile(file);
            }
            catch (Exception ex)
            {
                App.CurrentLogger?.Error(ex, "删除 DNS 账户失败");
                return false;
            }
        }
    }

    /// <summary>把账户凭据转换为 lego 需要的「环境变量名 → 值」字典。</summary>
    /// <returns>服务商未知或缺少必填字段时返回 null。</returns>
    public static Dictionary<string, string>? BuildEnvironment(DnsAccountEntry entry)
    {
        var descriptor = DnsProviders.Resolve(entry.Meta.Provider);
        if (descriptor is null)
        {
            App.CurrentLogger?.Warning($"未知的 DNS 服务商：{entry.Meta.Provider}");
            return null;
        }

        var env = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in descriptor.Fields)
        {
            entry.Credentials.TryGetValue(field.Key, out var value);
            value = value?.Trim();

            if (string.IsNullOrEmpty(value))
            {
                if (field.Required)
                {
                    App.CurrentLogger?.Warning($"DNS 账户缺少必填字段：{field.Key}");
                    return null;
                }

                continue;
            }

            env[field.EnvVar] = value;
        }

        return env.Count == 0 ? null : env;
    }

    /// <summary>读取并解密账户文件；文件缺失或损坏时返回空集合。</summary>
    private static DnsAccountFile LoadFile()
    {
        try
        {
            var path = StorePath;
            if (!File.Exists(path))
            {
                return new DnsAccountFile();
            }

            var raw = File.ReadAllBytes(path);
            var json = Decrypt(raw);
            var file = JsonSerializer.Deserialize(json, Json.DnsAccountFile);
            return file ?? new DnsAccountFile();
        }
        catch (Exception ex)
        {
            // 解密失败通常意味着本机密钥已变更（换机 / 重装 / 清理过 keys 目录）。
            // 此时不能静默重建文件，否则会无声覆盖旧账户；仅记录并返回空集合。
            App.CurrentLogger?.Error(ex, "读取 DNS 账户失败（可能本机密钥已变更）");
            return new DnsAccountFile();
        }
    }

    /// <summary>加密并写入账户文件。</summary>
    private static bool WriteFile(DnsAccountFile file)
    {
        try
        {
            Directory.CreateDirectory(StorageDirectory);
            var json = JsonSerializer.Serialize(file, Json.DnsAccountFile);
            var payload = Encrypt(json);

            var path = StorePath;
            File.WriteAllBytes(path, payload);

            // Unix 上收敛权限（尽力而为）
            if (!OperatingSystem.IsWindows())
            {
                try
                {
                    File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
                catch (Exception ex)
                {
                    App.CurrentLogger?.Warning($"设置 DNS 账户文件权限失败：{ex.Message}");
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Error(ex, "写入 DNS 账户失败");
            return false;
        }
    }

    /// <summary>把明文编码为「16 字节 IV + AES-256-CBC 密文」。</summary>
    private static byte[] Encrypt(string plainText)
    {
        var iv = RandomNumberGenerator.GetBytes(16);
        using var aes = Aes.Create();
        aes.Key = GetOrCreateKey();
        aes.IV = iv;

        using var ms = new MemoryStream();
        ms.Write(iv, 0, iv.Length);
        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        {
            var bytes = Encoding.UTF8.GetBytes(plainText);
            cs.Write(bytes, 0, bytes.Length);
        }

        return ms.ToArray();
    }

    /// <summary>解析「16 字节 IV + 密文」；格式非法或密钥不匹配时抛出。</summary>
    private static string Decrypt(byte[] payload)
    {
        if (payload.Length <= 16)
        {
            throw new CryptographicException("DNS 账户文件长度非法");
        }

        var iv = new byte[16];
        var cipher = new byte[payload.Length - 16];
        Buffer.BlockCopy(payload, 0, iv, 0, 16);
        Buffer.BlockCopy(payload, 16, cipher, 0, cipher.Length);

        using var aes = Aes.Create();
        aes.Key = GetOrCreateKey();
        aes.IV = iv;

        using var ms = new MemoryStream(cipher);
        using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using var sr = new StreamReader(cs, Encoding.UTF8);
        return sr.ReadToEnd();
    }

    /// <summary>
    ///     取得（或首次生成）本机加密密钥。Windows 下密钥文件由 DPAPI 保护，
    ///     只有当前用户可解密，复制到其他机器无效。
    /// </summary>
    private static byte[] GetOrCreateKey()
    {
        if (_cachedKey is not null)
        {
            return _cachedKey;
        }

        try
        {
            Directory.CreateDirectory(KeyDirectory);
            var keyPath = Path.Combine(KeyDirectory, KeyFileName);

            if (File.Exists(keyPath))
            {
                var stored = File.ReadAllBytes(keyPath);
                _cachedKey = OperatingSystem.IsWindows()
                    ? ProtectedData.Unprotect(stored, null, DataProtectionScope.CurrentUser)
                    : stored;
                return _cachedKey;
            }

            var newKey = RandomNumberGenerator.GetBytes(32);
            var toWrite = OperatingSystem.IsWindows()
                ? ProtectedData.Protect(newKey, null, DataProtectionScope.CurrentUser)
                : newKey;

            File.WriteAllBytes(keyPath, toWrite);

            if (!OperatingSystem.IsWindows())
            {
                try
                {
                    File.SetUnixFileMode(keyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
                catch
                {
                    // 权限设置失败不阻断（例如 FAT 分区）
                }
            }

            _cachedKey = newKey;
            return newKey;
        }
        catch (Exception ex)
        {
            throw new SecurityException("无法创建或读取 DNS 账户加密密钥", ex);
        }
    }
}
