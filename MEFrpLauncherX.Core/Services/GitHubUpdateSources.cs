namespace MEFrpLauncherX.Core.Services;

/// <summary>
///     更新下载源定义（26.4）。
///     <para>
///         与 <c>install.sh</c> 的线路设计保持一致：<c>TPCA</c> 走自建 Alist CDN；
///         其余线路走 GitHub Release，并通过镜像前缀加速「API + 资产下载」。
///     </para>
///     <para>
///         镜像线路的下载地址 = <see cref="GetDownloadPrefix" /> + 资产原始地址
///         （即 GitHub 返回的 <c>browser_download_url</c>），与 install.sh 中
///         <c>DOWNLOAD_URL="${GITHUB_MIRROR}${ASSET_URL}"</c> 的规则一致。
///     </para>
/// </summary>
public static class GitHubUpdateSources
{
    /// <summary>自建 Alist CDN（默认，不需要 GitHub）</summary>
    public const string Tpca = "TPCA";

    /// <summary>GitHub 官方线路（api.github.com + github.com 直连）</summary>
    public const string GitHub = "GitHub";

    /// <summary>GitHub 线路，经 gh-proxy 镜像加速</summary>
    public const string GitHubGhProxy = "GitHubGhProxy";

    /// <summary>GitHub 线路，经 moeyy 镜像加速</summary>
    public const string GitHubMoeyy = "GitHubMoeyy";

    /// <summary>全部合法取值；顺序即 UI 下拉框顺序</summary>
    public static readonly string[] All = [Tpca, GitHub, GitHubGhProxy, GitHubMoeyy];

    /// <summary>镜像加速站（下载链接 = 镜像站前缀 + GitHub 原始下载链接）</summary>
    private const string GhProxyMirror = "https://gh-proxy.org/";

    private const string MoeyyMirror = "https://github.moeyy.xyz/";

    /// <summary>
    ///     镜像站对应的 GitHub REST API 加速前缀。
    ///     与 <c>install.sh</c> 的 <c>GITHUB_API</c> 语义一致（通过镜像代理 GitHub API）。
    /// </summary>
    private const string GhProxyApiPrefix = "https://gh-proxy.org/api.github.com";

    private const string MoeyyApiPrefix = "https://github.moeyy.xyz/https://api.github.com";

    /// <summary>GitHub 官方 API</summary>
    private const string OfficialApi = "https://api.github.com";

    /// <summary>把配置中的任意取值归一化为合法值（非法 / 空值回落到 <see cref="Tpca" />）。</summary>
    public static string Normalize(string? value)
    {
        foreach (var candidate in All)
        {
            if (string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return Tpca;
    }

    /// <summary>该下载源是否需要访问 GitHub（即是否走 Release 资产）。</summary>
    public static bool IsGitHub(string? source) => Normalize(source) != Tpca;

    /// <summary>该下载源对应的 GitHub REST API 基址（不含末尾斜杠）。</summary>
    public static string GetApiBase(string? source) => Normalize(source) switch
    {
        GitHubGhProxy => GhProxyApiPrefix,
        GitHubMoeyy => MoeyyApiPrefix,
        _ => OfficialApi
    };

    /// <summary>
    ///     该下载源对应的镜像前缀（直接拼在 GitHub 下载链接之前）。
    ///     直连线路返回空串，即使用资产的原始地址。
    /// </summary>
    public static string GetDownloadPrefix(string? source) => Normalize(source) switch
    {
        GitHubGhProxy => GhProxyMirror,
        GitHubMoeyy => MoeyyMirror,
        _ => string.Empty
    };

    /// <summary>镜像站展示名（用于日志与状态文案，纯主机名，不含 scheme 与末尾斜杠）。</summary>
    public static string GetDisplayName(string? source) =>
        Normalize(source) switch
        {
            GitHubGhProxy => GetMirrorHost(GhProxyMirror),
            GitHubMoeyy => GetMirrorHost(MoeyyMirror),
            GitHub => "github.com",
            _ => Tpca
        };

    /// <summary>获取 API 线路的展示名（供日志排障使用）。</summary>
    public static string GetApiDisplayName(string apiBase) =>
        Uri.TryCreate(apiBase, UriKind.Absolute, out var uri) ? uri.Host : apiBase;

    /// <summary>从镜像前缀中提取主机名（去除 scheme 与路径）。</summary>
    private static string GetMirrorHost(string mirror)
    {
        if (!Uri.TryCreate(mirror, UriKind.Absolute, out var uri))
        {
            return mirror.TrimEnd('/');
        }

        return uri.Host;
    }

    /// <summary>把资产原始下载地址转换成当前下载源可用的地址。</summary>
    public static string BuildDownloadUrl(string? source, string browserDownloadUrl)
    {
        var prefix = GetDownloadPrefix(source);
        return string.IsNullOrEmpty(prefix) ? browserDownloadUrl : prefix + browserDownloadUrl;
    }
}
