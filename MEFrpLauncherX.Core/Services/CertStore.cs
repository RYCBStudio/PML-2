using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using MEFrpLauncherX.Core.Models;

namespace MEFrpLauncherX.Core.Services;

/// <summary>
///     证书仓库（26.4）：管理 <c>Config/Certificates/{slug}/</c> 下的证书落盘、读取与删除。
///     目录约定：
///     <code>
///     Config/Certificates/{slug}/
///       fullchain.pem   证书链（含叶证书），对应隧道的「证书路径」
///       privkey.pem     私钥，对应隧道的「密钥路径」
///       meta.json       域名 / 到期时间 / Staging / 签发时间
///     </code>
///     不依赖任何 UI 类型与第三方库（到期时间用 <see cref="X509Certificate2" /> 解析，AOT 安全）。
/// </summary>
public static class CertStore
{
    private static string RootDirectory
    {
        get;
    } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Certificates");

    /// <summary>证书根目录（供 UI 显示与「打开目录」）</summary>
    public static string RootPath => RootDirectory;

    /// <summary>把域名转换为安全的目录名（通配符与 . 统一替换）。</summary>
    public static string Slugify(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return "unknown";
        }

        var chars = domain.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_')
            .ToArray();
        return new string(chars);
    }

    /// <summary>指定 slug 的证书目录。</summary>
    public static string GetDirectory(string slug) => Path.Combine(RootDirectory, Slugify(slug));

    /// <summary>
    ///     从 lego 的产物目录复制并重命名为 <c>fullchain.pem</c> / <c>privkey.pem</c>。
    ///     lego 每个域名产出 <c>{domain}.crt</c>（<b>已含 CA 链</b>）、<c>{domain}.key</c>、
    ///     <c>{domain}.issuer.crt</c>、<c>{domain}.json</c>；其中 <c>.crt</c> 不能再与 issuer 拼接，
    ///     否则链会重复，因此这里只做重命名复制。
    /// </summary>
    /// <param name="legoCertificatesDir">lego 输出目录（<c>{--path}/certificates</c>）</param>
    /// <param name="domain">主域名（用于定位 lego 文件名）</param>
    /// <param name="meta">元数据（将写入 meta.json）</param>
    /// <returns>成功时返回证书目录；缺少必要文件时返回 null。</returns>
    public static string? SaveFromLego(string legoCertificatesDir, string domain, CertificateMeta meta)
    {
        try
        {
            var legoName = Slugify(domain);
            var crt = Path.Combine(legoCertificatesDir, $"{legoName}.crt");
            var key = Path.Combine(legoCertificatesDir, $"{legoName}.key");

            if (!File.Exists(crt) || !File.Exists(key))
            {
                App.CurrentLogger?.Warning($"lego 产物缺失：{crt} 或 {key} 不存在");
                return null;
            }

            var targetDir = GetDirectory(meta.SlugOrDomain());
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            var fullChainPath = Path.Combine(targetDir, meta.FullChainFile);
            var keyPath = Path.Combine(targetDir, meta.PrivateKeyFile);

            File.Copy(crt, fullChainPath, true);
            File.Copy(key, keyPath, true);

            // 私钥在 Unix 上收敛权限（尽力而为，失败仅记日志）
            if (!OperatingSystem.IsWindows())
            {
                try
                {
                    File.SetUnixFileMode(keyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
                catch (Exception ex)
                {
                    App.CurrentLogger?.Error(ex, "设置私钥文件权限失败");
                }
            }

            // 解析到期时间，便于后续做临期提醒
            meta.NotAfter = TryReadNotAfter(fullChainPath);
            File.WriteAllText(Path.Combine(targetDir, "meta.json"),
                JsonSerializer.Serialize(meta, App.AppJsonSerializerContext.CertificateMeta));

            return targetDir;
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Error(ex, "保存证书失败");
            return null;
        }
    }

    /// <summary>枚举全部本地证书（按域名排序）；目录缺失或损坏时跳过。</summary>
    public static List<CertificateListItem> List()
    {
        var result = new List<CertificateListItem>();
        try
        {
            if (!Directory.Exists(RootDirectory))
            {
                return result;
            }

            foreach (var dir in Directory.GetDirectories(RootDirectory))
            {
                var item = BuildItem(dir);
                if (item is not null)
                {
                    result.Add(item);
                }
            }
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Error(ex, "枚举本地证书失败");
        }

        return [.. result.OrderBy(x => x.Domain, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>读取单个证书目录的列表项；缺文件或元数据损坏时返回 null。</summary>
    private static CertificateListItem? BuildItem(string dir)
    {
        try
        {
            var fullChain = Path.Combine(dir, "fullchain.pem");
            var privateKey = Path.Combine(dir, "privkey.pem");
            if (!File.Exists(fullChain) || !File.Exists(privateKey))
            {
                return null;
            }

            var slug = Path.GetFileName(dir);
            var metaPath = Path.Combine(dir, "meta.json");
            var meta = File.Exists(metaPath)
                ? JsonSerializer.Deserialize<CertificateMeta>(File.ReadAllText(metaPath),
                    App.AppJsonSerializerContext.CertificateMeta)
                : null;

            var notAfter = meta?.NotAfter ?? TryReadNotAfter(fullChain);
            return new CertificateListItem
            {
                Slug = slug,
                Domain = string.IsNullOrWhiteSpace(meta?.Domain) ? slug : meta!.Domain,
                AltNamesText = meta?.AltNames is { Count: > 0 } ? string.Join(", ", meta.AltNames) : string.Empty,
                FullChainPath = fullChain,
                PrivateKeyPath = privateKey,
                NotAfter = notAfter,
                DaysToExpiry = notAfter is null
                    ? null
                    : (int)Math.Ceiling((notAfter.Value - DateTimeOffset.UtcNow).TotalDays),
                Staging = meta?.Staging ?? false
            };
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Error(ex, $"读取证书目录失败：{dir}");
            return null;
        }
    }

    /// <summary>删除指定 slug 的本地证书目录。</summary>
    public static bool Delete(string slug)
    {
        try
        {
            var dir = GetDirectory(slug);
            if (!Directory.Exists(dir))
            {
                return false;
            }

            Directory.Delete(dir, true);
            return true;
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Error(ex, "删除本地证书失败");
            return false;
        }
    }

    /// <summary>
    ///     解析 PEM 证书链的到期时间；解析失败返回 null（不抛出）。
    /// </summary>
    public static DateTimeOffset? TryReadNotAfter(string pemPath)
    {
        try
        {
            if (!File.Exists(pemPath))
            {
                return null;
            }

            // CreateFromPemFile 读取首个证书（即叶证书）
            using var cert = X509Certificate2.CreateFromPemFile(pemPath);
            return new DateTimeOffset(cert.NotAfter.ToUniversalTime());
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Warning($"解析证书到期时间失败：{ex.Message}");
            return null;
        }
    }
}
