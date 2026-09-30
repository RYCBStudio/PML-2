using System.Text.Json.Serialization;

namespace MEFrpLauncherX.Core.Models;

/// <summary>
///     GitHub REST API 返回的 Release 信息（26.4）。
///     仅保留更新下载所需的字段，避免反序列化无关内容。
///     <para>
///         参考接口：<c>GET /repos/{owner}/{repo}/releases/tags/{tag}</c>
///         （见 https://docs.github.com/rest/releases/releases#get-a-release-by-tag-name）
///     </para>
/// </summary>
public class GitHubRelease
{
    /// <summary>Release 标签名，例如 <c>v26.4.0-preview1</c></summary>
    [JsonPropertyName("tag_name")]
    public string? TagName
    {
        get;
        set;
    }

    /// <summary>Release 标题，例如 <c>PML 2 26.4.0-preview1 (Pre-release)</c></summary>
    [JsonPropertyName("name")]
    public string? Name
    {
        get;
        set;
    }

    /// <summary>是否为预发布版本</summary>
    [JsonPropertyName("prerelease")]
    public bool Prerelease
    {
        get;
        set;
    }

    /// <summary>发布时间（ISO 8601）</summary>
    [JsonPropertyName("published_at")]
    public string? PublishedAt
    {
        get;
        set;
    }

    /// <summary>Release 正文（Markdown）</summary>
    [JsonPropertyName("body")]
    public string? Body
    {
        get;
        set;
    }

    /// <summary>
    ///     API 错误信息（如 <c>Not Found</c> / <c>API rate limit exceeded</c>）。
    ///     仅在请求失败而非 2xx 时由 GitHub 返回，便于日志排障。
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message
    {
        get;
        set;
    }

    /// <summary>该 Release 携带的全部资产（安装包）</summary>
    [JsonPropertyName("assets")]
    public List<GitHubAsset> Assets
    {
        get;
        set;
    } = [];
}

/// <summary>
///     GitHub Release 中的单个资产（发布附件）。
/// </summary>
public class GitHubAsset
{
    /// <summary>资产文件名，例如 <c>pml2_setup.26.3.1.AOT.exe</c></summary>
    [JsonPropertyName("name")]
    public string? Name
    {
        get;
        set;
    }

    /// <summary>资产字节数</summary>
    [JsonPropertyName("size")]
    public long Size
    {
        get;
        set;
    }

    /// <summary>资产 MIME 类型</summary>
    [JsonPropertyName("content_type")]
    public string? ContentType
    {
        get;
        set;
    }

    /// <summary>
    ///     资产原始下载地址（始终指向 <c>https://github.com/...</c>）。
    ///     镜像线路通过「镜像前缀 + 本字段」拼接加速地址。
    /// </summary>
    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl
    {
        get;
        set;
    }
}
