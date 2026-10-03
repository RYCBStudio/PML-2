using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MEFrpLauncherX.Core;

namespace MEFrpLauncherX.Services;

/// <summary>
///     PML 2 26.5.0 匿名使用统计（Cloudflare Analytics Engine）。
///     <para>
///         第一阶段只上报 <c>startup</c> 事件，用于统计活跃安装数量、软件版本、操作系统平台与 CPU 架构。
///     </para>
///     <para>
///         核心原则：<b>Telemetry must never affect the application.</b>
///         网络 / 序列化 / 配置异常一律在内部吞掉，绝不向调用方抛出；不重试、不建立离线队列、不刷日志。
///     </para>
///     <para>
///         隐私边界：只上报服务端定义的 5 个字段。 <c>installation_id</c> 为随机 UUID，
///         不由用户名、邮箱、计算机名、MAC 地址、硬盘序列号、Windows SID、IP 地址或上述信息的哈希推导而来。
///     </para>
/// </summary>
public static class TelemetryService
{
    /// <summary>事件端点（服务端 v1 接口）。</summary>
    internal const string Endpoint = "https://telemetry.rycb.tech/v1/event";

    /// <summary>短超时：遥测不允许拖慢启动流程。</summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    ///     遥测专用客户端。项目内暂无通用 HTTP 基础设施，故与 <see cref="MEFrpLauncherX.Core.Services.GitHubReleaseService" />
    ///     一致地自建静态实例（避免每次上报新建连接池）。
    /// </summary>
    private static readonly HttpClient DefaultClient = CreateClient();

    /// <summary>
    ///     仅供 <c>tools/TelemetryVerify</c> 注入本地假客户端做离线验收；生产运行恒为 <c>null</c>，
    ///     不会改变真实上报目标（端点见 <see cref="Endpoint" />）。
    /// </summary>
#pragma warning disable CS0649 // 由 tools/TelemetryVerify 通过反射赋值，本程序集内无写入点
    internal static HttpClient? HttpClientOverride;
#pragma warning restore CS0649

    private static HttpClient Client => HttpClientOverride ?? DefaultClient;

    /// <summary>
    ///     上报一条 <c>startup</c> 事件。
    ///     <para>
    ///         必须 fire-and-forget 调用（<c>_ = TelemetryService.TrackStartupAsync();</c>）：
    ///         内部在线程池执行，配置落盘与 HTTP 都不会阻塞 Splash、MainWindow 创建、插件加载或启动流程。
    ///     </para>
    /// </summary>
    public static async Task TrackStartupAsync()
    {
        try
        {
            await Task.Run(TrackStartupCoreAsync).ConfigureAwait(false);
        }
        catch
        {
            // Telemetry must never affect the application.
        }
    }

    private static async Task TrackStartupCoreAsync()
    {
        // 1. 开关：仅在用户同意（IsTelemetryEnabled）时上报。
        //    关闭时：不发送事件、不生成 installation_id、不进行任何网络请求。
        if (!ConfigManager.CurrentConfig.IsTelemetryEnabled)
        {
            return;
        }

        // 2. installation_id：随机 UUID，首次生成后持久化，同一安装正常情况下长期复用。
        var installationId = GetOrCreateInstallationId();
        if (string.IsNullOrEmpty(installationId))
        {
            return;
        }

        // 3. payload：严格使用服务端定义的字段，不追加任何额外信息。
        var payload = new TelemetryEventPayload
        {
            Event = "startup",
            InstallationId = installationId,
            Version = Core.App.Version,
            Platform = GetPlatform(),
            Architecture = GetArchitecture()
        };

        // 4. POST JSON：非 2xx 直接忽略（不读内容、不重试、不排队、不记日志）。
        using var content = new StringContent(
            JsonSerializer.Serialize(payload, TelemetryJsonContext.Default.TelemetryEventPayload),
            Encoding.UTF8,
            "application/json");
        using var response = await Client.PostAsync(Endpoint, content).ConfigureAwait(false);
    }

    /// <summary>
    ///     读取已持久化的 <c>installation_id</c>；缺失或被用户清空 / 损坏时生成新的随机 UUID 并写回配置。
    /// </summary>
    private static string GetOrCreateInstallationId()
    {
        if (Guid.TryParse(ConfigManager.CurrentConfig.TelemetryInstallationId, out var existing) &&
            existing != Guid.Empty)
        {
            return existing.ToString("D");
        }

        var generated = Guid.NewGuid().ToString("D");
        // 落盘失败也不会抛出（ConfigManager 内部已捕获）；此时本次使用临时 id，下次启动再生成。
        ConfigManager.UpdateConfig(cfg => cfg.TelemetryInstallationId = generated);
        return generated;
    }

    /// <summary>操作系统平台标识（与服务端 / 隐私政策一致）。</summary>
    private static string GetPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return "windows";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "macos";
        }

        return OperatingSystem.IsLinux() ? "linux" : "unknown";
    }

    /// <summary>CPU 架构标识（与服务端 / 隐私政策一致）。</summary>
    private static string GetArchitecture() => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X64 => "x64",
        Architecture.Arm64 => "arm64",
        Architecture.X86 => "x86",
        Architecture.Arm => "arm",
        _ => "unknown"
    };

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = RequestTimeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"RYCB-PML2/{Core.App.Version} Desktop");
        return client;
    }
}

/// <summary>
///     <c>startup</c> 事件请求体。字段与服务端 v1 接口及隐私政策严格一致，不得追加字段。
/// </summary>
public sealed class TelemetryEventPayload
{
    [JsonPropertyName("event")]
    public string Event
    {
        get;
        set;
    } = string.Empty;

    [JsonPropertyName("installation_id")]
    public string InstallationId
    {
        get;
        set;
    } = string.Empty;

    [JsonPropertyName("version")]
    public string Version
    {
        get;
        set;
    } = string.Empty;

    [JsonPropertyName("platform")]
    public string Platform
    {
        get;
        set;
    } = string.Empty;

    [JsonPropertyName("architecture")]
    public string Architecture
    {
        get;
        set;
    } = string.Empty;
}

/// <summary>
///     遥测专用的 JSON 源生成上下文。
///     <para>
///         刻意独立于 <c>MEFrpLauncherX.AppJsonSerializerContext</c>：后者由 <c>App.Initialize()</c>
///         在 Avalonia 启动流程中构造，而遥测在 <c>OnFrameworkInitializationCompleted</c> 中以
///         fire-and-forget 方式触发，且必须「在任何初始化顺序下都能独立完成序列化」。
///         使用本上下文可保证遥测不引入任何启动顺序耦合（AOT 下也无需反射）。
///     </para>
/// </summary>
[JsonSerializable(typeof(TelemetryEventPayload))]
internal partial class TelemetryJsonContext : JsonSerializerContext
{
}
