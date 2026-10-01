using System.Net;
using System.Text.Json;
using MEFrpLauncherX.Core.Models;

namespace MEFrpLauncherX.Core.Services;

/// <summary>
///     GitHub Release 查询与资产挑选（26.4）。
///     <para>
///         设计要点：
///         <list type="bullet">
///             <item>API 依次尝试「当前线路 → 其他镜像线路 → 官方」，任一成功即返回（对齐 install.sh 的多源回退）；</item>
///             <item>tag 先试 <c>v{version}</c> 再试 <c>{version}</c>；</item>
///             <item>结果写入统一缓存（5 分钟），避免反复消耗 GitHub 匿名接口额度；</item>
///             <item>资产挑选按「平台 / 架构」硬过滤，再按「版本号、编译类型、包格式」打分，
///                   并支持排除指定资产（供下载失败后「重试」挑选替代包）。</item>
///         </list>
///     </para>
/// </summary>
public static class GitHubReleaseService
{
    /// <summary>GitHub 组织 / 用户</summary>
    public const string Owner = "RYCBStudio";

    /// <summary>GitHub 仓库名</summary>
    public const string Repo = "PML-2";

    private static readonly HttpClient Client = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"RYCB-PML2/{App.Version} Desktop");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    /// <summary>
    ///     按线路优先级列出候选 API 基址：当前线路优先，其余 GitHub 线路作为回退。
    /// </summary>
    private static IEnumerable<string> EnumerateApiBases(string? source)
    {
        var preferred = GitHubUpdateSources.GetApiBase(source);
        yield return preferred;

        foreach (var candidate in GitHubUpdateSources.All)
        {
            if (!GitHubUpdateSources.IsGitHub(candidate))
            {
                continue;
            }

            var apiBase = GitHubUpdateSources.GetApiBase(candidate);
            if (!string.Equals(apiBase, preferred, StringComparison.OrdinalIgnoreCase))
            {
                yield return apiBase;
            }
        }
    }

    /// <summary>
    ///     获取指定版本的 GitHub Release。
    /// </summary>
    /// <param name="version">版本号（不含前导 v），例如 <c>26.4.0-preview1</c></param>
    /// <param name="source">下载源（决定优先使用的 API 线路）</param>
    /// <param name="forceRefresh">true 表示跳过 5 分钟缓存强制请求</param>
    /// <returns>Release 信息；全部线路失败时返回 null（原因已记日志）</returns>
    public static async Task<GitHubRelease?> GetReleaseAsync(string version, string? source,
        bool forceRefresh = false)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        // tag 可能是 v{x} 或 {x}，两种都试（对齐 install.sh 的 github_fetch_release）
        foreach (var tag in new[] { $"v{version}", version })
        {
            var cacheKey = ApiCacheKeys.GitHubReleasePrefix + tag;
            if (!forceRefresh && TryReadCache(cacheKey, out var cached))
            {
                return cached;
            }

            foreach (var apiBase in EnumerateApiBases(source))
            {
                var release = await FetchAsync(apiBase, tag, cacheKey);
                if (release is not null)
                {
                    return release;
                }
            }
        }

        App.CurrentLogger?.Warning($"无法从 GitHub 获取版本 {version} 的 Release（已尝试全部线路）",
            module: EnumLogModule.Update);
        return null;
    }

    /// <summary>读取统一缓存中的 Release（命中时重新反序列化，返回独立实例）。</summary>
    private static bool TryReadCache(string cacheKey, out GitHubRelease? release)
    {
        release = null;
        if (!ApiCacheService.TryGetContent(cacheKey, out var cached))
        {
            return false;
        }

        try
        {
            release = JsonSerializer.Deserialize<GitHubRelease>(cached, App.AppJsonSerializerContext.GitHubRelease);
            if (release is null)
            {
                ApiCacheService.Invalidate(cacheKey);
                return false;
            }

            App.CurrentLogger?.LogDebug($"[缓存命中] {cacheKey}（5 分钟内不重复请求）",
                module: EnumLogModule.Update);
            return true;
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Error(ex, $"读取 GitHub Release 缓存失败: {cacheKey}");
            ApiCacheService.Invalidate(cacheKey);
            return false;
        }
    }

    /// <summary>请求单个 API 基址并解析 Release；失败返回 null。</summary>
    private static async Task<GitHubRelease?> FetchAsync(string apiBase, string tag, string cacheKey)
    {
        var url = $"{apiBase.TrimEnd('/')}/repos/{Owner}/{Repo}/releases/tags/{tag}";
        try
        {
            App.CurrentLogger?.LogDebug($"GET {url}", port: EnumLogPort.Server, module: EnumLogModule.Update);

            using var response = await Client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            // 额度信息（GitHub 匿名调用有频率限制），便于排障
            if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining))
            {
                App.CurrentLogger?.LogDebug($"GitHub API 剩余额度: {string.Join(",", remaining)}",
                    module: EnumLogModule.Update);
            }

            if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(content))
            {
                var hint = response.StatusCode is HttpStatusCode.Forbidden or (HttpStatusCode)429
                    ? "（可能触发 GitHub 频率限制，可切换其他下载源）"
                    : string.Empty;
                App.CurrentLogger?.Warning(
                    $"GitHub API 请求失败 ({GitHubUpdateSources.GetApiDisplayName(apiBase)}, {tag}): " +
                    $"HTTP {(int)response.StatusCode}{hint}",
                    module: EnumLogModule.Update);
                return null;
            }

            var release = JsonSerializer.Deserialize<GitHubRelease>(content,
                App.AppJsonSerializerContext.GitHubRelease);
            if (release?.TagName is null)
            {
                // 例如 {"message":"Not Found"}：该 tag 在本线路不存在
                App.CurrentLogger?.LogDebug($"GitHub API 未返回有效 Release（{tag}）: {release?.Message}",
                    module: EnumLogModule.Update);
                return null;
            }

            ApiCacheService.SetContent(cacheKey, content);
            App.CurrentLogger?.Log($"已获取 GitHub Release: {release.TagName}（{release.Assets.Count} 个资产）",
                module: EnumLogModule.Update);
            return release;
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Warning(
                $"GitHub API 请求异常 ({GitHubUpdateSources.GetApiDisplayName(apiBase)}): {ex.Message}",
                module: EnumLogModule.Update);
            return null;
        }
    }

    /// <summary>
    ///     从 Release 中挑选与当前运行环境匹配的安装包。
    ///     <br />
    ///     硬过滤：平台与 CPU 架构必须匹配；软打分：版本号、编译类型、包格式、在线包优先。
    /// </summary>
    /// <param name="release">Release 信息</param>
    /// <param name="version">目标版本号，用于优先选择文件名含完整版本号的资产</param>
    /// <param name="compileType">目标编译类型（<c>AOT</c> / <c>Common</c>）</param>
    /// <param name="excludedNames">需要排除的资产名（例如上次下载失败的资产，供「重试」挑选替代包）</param>
    /// <returns>最匹配的资产；无匹配时返回 null</returns>
    public static GitHubAsset? SelectAsset(GitHubRelease release, string version, string? compileType,
        IReadOnlyCollection<string>? excludedNames = null)
    {
        var platform = GetPlatformTag();
        var arch = GetArchTag();
        if (platform is null || arch is null)
        {
            App.CurrentLogger?.Warning(
                $"当前平台（{System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier}）没有对应的 GitHub 资产",
                module: EnumLogModule.Update);
            return null;
        }

        var wantAot = !string.Equals(compileType, "Common", StringComparison.OrdinalIgnoreCase);
        var shortVersion = GetShortVersion(version);

        GitHubAsset? best = null;
        var bestScore = int.MinValue;

        foreach (var asset in release.Assets)
        {
            if (string.IsNullOrWhiteSpace(asset.Name) || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
            {
                continue;
            }

            if (excludedNames is not null && excludedNames.Contains(asset.Name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var score = ScoreAsset(asset.Name, platform, arch, version, shortVersion, wantAot);
            if (score < 0)
            {
                continue; // 平台 / 架构不匹配，直接跳过
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = asset;
            }
        }

        if (best is null)
        {
            var reason = excludedNames is { Count: > 0 } ? "（已排除上次失败的资产）" : string.Empty;
            App.CurrentLogger?.Warning(
                $"Release {release.TagName} 中没有匹配 {platform}-{arch} 的安装包" +
                $"（目标编译类型: {(wantAot ? "AOT" : "Common")}）{reason}",
                module: EnumLogModule.Update);
            return null;
        }

        App.CurrentLogger?.Log($"已选择 GitHub 资产: {best.Name}（{best.Size} 字节，评分 {bestScore}）",
            module: EnumLogModule.Update);
        return best;
    }

    /// <summary>
    ///     为资产打分。返回负值表示平台 / 架构不匹配（不可用）。
    /// </summary>
    private static int ScoreAsset(string name, string platform, string arch, string version, string shortVersion,
        bool wantAot)
    {
        var lower = name.ToLowerInvariant();

        // ---------- 1. 平台硬过滤（按文件扩展名判定） ----------
        var platformMatched = platform switch
        {
            "windows" => lower.EndsWith(".exe", StringComparison.Ordinal),
            "macos" => lower.EndsWith(".dmg", StringComparison.Ordinal),
            "linux" => lower.EndsWith(".deb", StringComparison.Ordinal) ||
                       lower.EndsWith(".rpm", StringComparison.Ordinal) ||
                       lower.EndsWith(".tar.gz", StringComparison.Ordinal) ||
                       lower.EndsWith(".tar.zst", StringComparison.Ordinal),
            _ => false
        };
        if (!platformMatched)
        {
            return -1;
        }

        // ---------- 2. 架构硬过滤 ----------
        var isArm = arch == "arm64";
        if (isArm != lower.Contains("arm64", StringComparison.Ordinal))
        {
            return -1;
        }

        var score = 10;

        // ---------- 3. 版本号：文件名含完整版本号（含 -previewN）优先级最高 ----------
        if (!string.IsNullOrEmpty(version) && lower.Contains(version.ToLowerInvariant(), StringComparison.Ordinal))
        {
            score += 100;
        }
        else if (!string.IsNullOrEmpty(shortVersion) &&
                 lower.Contains(shortVersion.ToLowerInvariant(), StringComparison.Ordinal))
        {
            score += 40;
        }

        // ---------- 4. 编译类型：AOT 期待 aot 标记，Common 期待 r2r / 无标记 ----------
        var hasAot = lower.Contains("aot", StringComparison.Ordinal);
        var hasR2R = lower.Contains("r2r", StringComparison.Ordinal);
        score += wantAot
            ? hasAot ? 30 : -20
            : hasR2R ? 30 : hasAot ? -20 : 0;

        // ---------- 5. 包格式偏好（Linux 优先 deb，与 TPCA 线路的历史行为一致） ----------
        score += platform switch
        {
            "linux" when lower.EndsWith(".deb", StringComparison.Ordinal) => 20,
            "linux" when lower.EndsWith(".rpm", StringComparison.Ordinal) => 10,
            _ => 0
        };

        // ---------- 6. 在线安装包优先于离线包（体积更小，与既有行为一致） ----------
        if (lower.Contains("offline", StringComparison.Ordinal))
        {
            score -= 15;
        }

        return score;
    }

    /// <summary>把 <c>26.4.0-preview1</c> 归约为 <c>26.4.0</c>，供「粗匹配」文件名使用。</summary>
    private static string GetShortVersion(string version)
    {
        try
        {
            var parsed = VersionComparer.ParseVersion(version);
            return $"{parsed.Major}.{parsed.Minor}.{parsed.Patch}";
        }
        catch (ArgumentException)
        {
            return version;
        }
    }

    /// <summary>当前平台的资产平台标识（与 GitHub 资产命名保持一致）。</summary>
    private static string? GetPlatformTag()
    {
        if (OperatingSystem.IsWindows())
        {
            return "windows";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "macos";
        }

        return OperatingSystem.IsLinux() ? "linux" : null;
    }

    /// <summary>当前 CPU 架构的资产标识。</summary>
    private static string? GetArchTag() =>
        System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.X64 => "x64",
            System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
            _ => null
        };

    /// <summary>
    ///     根据资产文件名推断安装包扩展名（小写，不含点），供保存临时文件与安装提示使用。
    /// </summary>
    public static string PickInstallExtension(string assetName)
    {
        var lower = assetName.ToLowerInvariant();
        foreach (var ext in new[] { ".tar.gz", ".tar.zst", ".deb", ".rpm", ".dmg", ".exe" })
        {
            if (lower.EndsWith(ext, StringComparison.Ordinal))
            {
                return ext.TrimStart('.');
            }
        }

        var dot = lower.LastIndexOf('.');
        return dot >= 0 && dot < lower.Length - 1 ? lower[(dot + 1)..] : "bin";
    }
}
