﻿using System.Text.RegularExpressions;
using MEFrpLauncherX.Core.Models;

namespace MEFrpLauncherX.Core.Services;

/// <summary>
///     证书申请状态机阶段（26.4 阶段 B）。
///     DNS 账号自动模式下依次经过 Present → 传播校验 → 出证 → CleanUp；
///     手动模式会额外停留在 <see cref="WaitingUserConfirm" /> 等待用户添加 TXT。
/// </summary>
public enum AcmeStage
{
    /// <summary>尚未开始</summary>
    Idle,

    /// <summary>检查 / 下载 lego</summary>
    CheckingLego,

    /// <summary>正在下发挑战记录（lego 调用 DNS API 写入 TXT）</summary>
    RunningPresent,

    /// <summary>已拿到 TXT 记录，等待用户到 DNS 服务商手动添加（仅手动模式）</summary>
    WaitingUserConfirm,

    /// <summary>等待 DNS 传播并由 CA 校验</summary>
    WaitingPropagation,

    /// <summary>校验通过，正在出证</summary>
    ObtainingCert,

    /// <summary>正在清理挑战记录</summary>
    CleaningUp,

    /// <summary>签发成功</summary>
    Succeeded,

    /// <summary>失败</summary>
    Failed
}

/// <summary>一次证书申请的进度回调载荷</summary>
/// <param name="Stage">当前阶段</param>
/// <param name="Percent">总体进度（0–100）</param>
/// <param name="ChallengeHost">TXT 记录主机名（仅手动模式）</param>
/// <param name="ChallengeValue">TXT 记录值（仅手动模式）</param>
/// <param name="Message">可读状态文案</param>
/// <param name="Log">已脱敏的原文日志行，供界面实时追加（可空）</param>
public sealed record AcmeProgress(
    AcmeStage Stage,
    double Percent,
    string? ChallengeHost = null,
    string? ChallengeValue = null,
    string? Message = null,
    string? Log = null);

/// <summary>证书申请的验证方式。</summary>
public enum AcmeChallengeMode
{
    /// <summary>使用已保存的 DNS 账户自动完成 DNS-01（一键，阶段 B 新增）</summary>
    DnsAccount,

    /// <summary>手动 DNS-01（阶段 A，保留）</summary>
    Manual
}

/// <summary>证书申请请求。</summary>
public sealed record AcmeRequest
{
    /// <summary>主域名（如 <c>example.com</c>）</summary>
    public required string Domain { get; init; }

    /// <summary>附加域名 / SAN（可含 <c>*.example.com</c>）</summary>
    public IReadOnlyList<string> AltNames { get; init; } = [];

    /// <summary>ACME 账户邮箱</summary>
    public required string Email { get; init; }

    /// <summary>是否使用 Staging 环境（默认 true，避免消耗生产配额）</summary>
    public bool Staging { get; init; } = true;

    /// <summary>验证方式</summary>
    public AcmeChallengeMode Mode { get; init; } = AcmeChallengeMode.DnsAccount;

    /// <summary>所选 DNS 账户（<see cref="AcmeChallengeMode.DnsAccount" /> 时必填）</summary>
    public Guid? DnsAccountId { get; init; }

    /// <summary>DNS 账户展示名（仅用于日志与错误提示，不含凭据）</summary>
    public string? DnsAccountDisplayName { get; init; }

    /// <summary>高级选项：跳过 DNS 传播检查（默认关闭）</summary>
    public bool SkipPropagationCheck { get; init; }

    /// <summary>DNS 传播等待上限（秒）</summary>
    public int PropagationTimeoutSeconds { get; init; } =
        AcmeCertificateService.DefaultPropagationTimeoutSeconds;
}

/// <summary>证书申请结果。</summary>
/// <param name="Success">是否成功</param>
/// <param name="CertificateDirectory">成功时的证书目录（含 fullchain.pem / privkey.pem / meta.json）</param>
/// <param name="Message">失败原因的<b>用户可读</b>文案</param>
/// <param name="ErrorKind">失败分类（成功时为 <see cref="AcmeErrorKind.Unknown" />）</param>
/// <param name="RawLog">已脱敏的 lego 输出，便于用户排查</param>
public sealed record AcmeResult(
    bool Success,
    string? CertificateDirectory,
    string? Message,
    AcmeErrorKind ErrorKind = AcmeErrorKind.Unknown,
    string? RawLog = null);

/// <summary>
///     ACME 证书签发服务（26.4 阶段 B）。
///     两种验证方式共用同一状态机：
///     <list type="bullet">
///         <item><b>DNS 账号</b>（默认）：读取本地加密的 DNS 账户，交给 lego 一键完成
///               Present → 传播校验 → 出证 → CleanUp，全程无需手改解析；</item>
///         <item><b>手动 DNS</b>（阶段 A）：lego 输出 TXT 记录，由用户添加后确认继续。</item>
///     </list>
///     设计约束：
///     <list type="bullet">
///         <item>不引入托管 ACME 库，避免 AOT/裁剪风险（lego 为独立 Go 二进制，永不链进主程序）；</item>
///         <item>凭据仅注入 lego 子进程的环境变量，输出统一经 <see cref="SecretRedactor" /> 脱敏；</item>
///         <item>不使用 HTTP-01，泛域名天然可用。</item>
///     </list>
/// </summary>
public static partial class AcmeCertificateService
{
    /// <summary>Let's Encrypt 生产目录</summary>
    public const string LetsEncryptProduction = "https://acme-v02.api.letsencrypt.org/directory";

    /// <summary>Let's Encrypt 测试目录（默认使用，签发速度快且不消耗生产配额）</summary>
    public const string LetsEncryptStaging = "https://acme-staging-v02.api.letsencrypt.org/directory";

    /// <summary>DNS 传播等待默认上限（秒）</summary>
    public const int DefaultPropagationTimeoutSeconds = 300;

    private static string WorkRoot { get; } = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "Tools", "lego");

    /// <summary>lego 的工作目录（账户密钥与证书都落在这里，避免污染用户配置目录）</summary>
    public static string WorkPath => Path.Combine(WorkRoot, "accounts");

    /// <summary>签发证书。</summary>
    /// <param name="request">签发请求</param>
    /// <param name="confirmChallenge">
    ///     手动模式下的确认回调：收到 TXT 记录信息后被调用，返回 true 表示继续。
    ///     DNS 账号模式不会调用该回调（可传 null）。
    /// </param>
    /// <param name="progress">进度回调</param>
    /// <param name="ct">取消令牌</param>
    public static async Task<AcmeResult> IssueAsync(
        AcmeRequest request,
        Func<AcmeProgress, Task<bool>>? confirmChallenge = null,
        Action<AcmeProgress>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            // 1. 准备 lego
            progress?.Invoke(new AcmeProgress(AcmeStage.CheckingLego, 2, Message: "正在准备 lego…"));
            var legoPath = await LegoRunner.EnsureLegoAsync(
                (percent, message) => progress?.Invoke(
                    new AcmeProgress(AcmeStage.CheckingLego, percent * 0.2, Message: message)),
                ct);
            if (legoPath is null)
            {
                return Fail(AcmeErrorKind.LegoUnavailable, "Text.Dns.Error.LegoUnavailable");
            }

            // 2. 解析 DNS 服务商与凭据（凭据只在本进程内存与子进程环境中存在）
            var isDnsApi = request.Mode == AcmeChallengeMode.DnsAccount;
            var provider = "manual";
            IReadOnlyDictionary<string, string>? providerEnv = null;
            var envPrefix = string.Empty;

            if (isDnsApi)
            {
                var resolved = ResolveProvider(request);
                if (resolved is null)
                {
                    return Fail(AcmeErrorKind.DnsAuthFailed, "Text.Dns.Error.AccountUnusable");
                }

                (provider, providerEnv, envPrefix) = resolved.Value;
            }

            progress?.Invoke(new AcmeProgress(
                isDnsApi ? AcmeStage.RunningPresent : AcmeStage.WaitingUserConfirm, 25,
                Message: isDnsApi ? "正在通过 DNS API 下发验证记录…" : "正在向 CA 下单…"));

            return await ExecuteAsync(request, confirmChallenge, progress, ct, legoPath,
                provider, providerEnv, envPrefix);
        }
        catch (OperationCanceledException)
        {
            return new AcmeResult(false, null, AcmeErrorMapper.GetMessage(
                new AcmeErrorInfo(AcmeErrorKind.Unknown, "Text.Dns.Error.Cancelled")));
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Error(ex, "证书申请异常");
            return new AcmeResult(false, null, ex.Message);
        }
    }

    /// <summary>执行 lego 调用并把产物归档到 <see cref="CertStore" />。</summary>
    private static async Task<AcmeResult> ExecuteAsync(
        AcmeRequest request,
        Func<AcmeProgress, Task<bool>>? confirmChallenge,
        Action<AcmeProgress>? progress,
        CancellationToken ct,
        string legoPath,
        string provider,
        IReadOnlyDictionary<string, string>? providerEnv,
        string envPrefix)
    {
        var isManual = request.Mode == AcmeChallengeMode.Manual;

        var runRequest = new LegoRunRequest
        {
            Mode = isManual ? LegoChallengeMode.Manual : LegoChallengeMode.DnsApi,
            Domain = request.Domain,
            AltNames = request.AltNames,
            Email = request.Email,
            Staging = request.Staging,
            LegoProvider = provider,
            ProviderEnvironment = providerEnv,
            ProviderEnvPrefix = envPrefix,
            WorkPath = WorkPath,
            PropagationTimeoutSeconds = request.PropagationTimeoutSeconds,
            SkipPropagationCheck = request.SkipPropagationCheck
        };

        // 手动模式需要把 lego 打印的 TXT 记录解析出来展示给用户
        string? challengeHost = null;
        string? challengeValue = null;

        var runResult = await LegoRunner.RunAsync(
            legoPath,
            runRequest,
            line =>
            {
                if (isManual)
                {
                    ParseChallengeLine(line, ref challengeHost, ref challengeValue);
                }

                progress?.Invoke(new AcmeProgress(MapLogToStage(line, isManual), 60,
                    challengeHost, challengeValue, Message: null, Log: line));
            },
            isManual && confirmChallenge is not null
                ? async () =>
                {
                    var snapshot = new AcmeProgress(AcmeStage.WaitingUserConfirm, 55,
                        challengeHost, challengeValue, "请在 DNS 服务商添加 TXT 记录后继续");
                    progress?.Invoke(snapshot);
                    return await confirmChallenge(snapshot);
                }
                : null,
            ct);

        if (!runResult.Success)
        {
            var error = runResult.Error ?? AcmeErrorMapper.Map(runResult.StandardError);
            App.CurrentLogger?.Warning(
                $"证书申请失败（{error.Kind}，退出码 {runResult.ExitCode}）：{runResult.StandardError}");

            return new AcmeResult(false, null, AcmeErrorMapper.GetMessage(error), error.Kind,
                BuildLog(runResult));
        }

        // 整理产物：lego 的 {domain}.crt 已含 CA 链，直接重命名为 fullchain.pem
        progress?.Invoke(new AcmeProgress(AcmeStage.ObtainingCert, 90,
            challengeHost, challengeValue, "正在整理证书文件…"));

        var meta = new CertificateMeta
        {
            Domain = request.Domain,
            AltNames = [.. request.AltNames],
            Staging = request.Staging,
            IssuedAt = DateTimeOffset.UtcNow,
            AcmeServer = request.Staging ? LetsEncryptStaging : LetsEncryptProduction,
            LegoVersion = LegoDownloader.LegoVersion,
            DnsAccountId = request.DnsAccountId,
            ChallengeMode = isManual ? "Manual" : "DnsApi"
        };

        var dir = CertStore.SaveFromLego(runResult.CertificatesDirectory, request.Domain, meta);
        if (dir is null)
        {
            return Fail(AcmeErrorKind.Unknown, "Text.Dns.Error.CertSaveFailed", BuildLog(runResult));
        }

        progress?.Invoke(new AcmeProgress(AcmeStage.Succeeded, 100, challengeHost, challengeValue, "签发完成"));
        App.CurrentLogger?.Info(
            $"证书签发成功：{request.Domain}（{(request.Staging ? "staging" : "production")}）");

        return new AcmeResult(true, dir, null);
    }

    /// <summary>
    ///     读取本地加密的 DNS 账户，返回「lego provider 代码 / 环境变量 / 环境变量前缀」。
    ///     任一步缺失都返回 null（凭据不足时不发起申请）。
    /// </summary>
    private static (string Provider, IReadOnlyDictionary<string, string> Env, string Prefix)? ResolveProvider(
        AcmeRequest request)
    {
        if (request.DnsAccountId is not { } id)
        {
            App.CurrentLogger?.Warning("DNS 账号模式未指定账户");
            return null;
        }

        var entry = DnsAccountStore.Get(id);
        if (entry is null)
        {
            App.CurrentLogger?.Warning($"DNS 账户不存在：{id}");
            return null;
        }

        var descriptor = DnsProviders.Resolve(entry.Meta.Provider);
        if (descriptor is null)
        {
            App.CurrentLogger?.Warning($"DNS 账户的服务商不受支持：{entry.Meta.Provider}");
            return null;
        }

        var env = DnsAccountStore.BuildEnvironment(entry);
        if (env is null)
        {
            App.CurrentLogger?.Warning("DNS 账户凭据不完整，已中止申请");
            return null;
        }

        // 只记录变量名，绝不记录取值
        App.CurrentLogger?.Info(
            $"使用 DNS 账户「{entry.Meta.DisplayName}」（{descriptor.DisplayName}）：" +
            SecretRedactor.DescribeEnvironment(env));

        return (descriptor.LegoProvider, env, descriptor.EnvPrefix);
    }

    /// <summary>构造失败结果，并把资源键解析为当前语言的文案。</summary>
    private static AcmeResult Fail(AcmeErrorKind kind, string resourceKey, string? rawLog = null)
    {
        var message = AcmeErrorMapper.GetMessage(new AcmeErrorInfo(kind, resourceKey));
        return new AcmeResult(false, null, message, kind, rawLog);
    }

    /// <summary>把已脱敏的 lego 输出拼接为可折叠的排查日志（限制长度避免界面卡顿）。</summary>
    private static string? BuildLog(LegoRunResult result)
    {
        const int maxLength = 4000;

        var text = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : result.StandardError;
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        text = text.Trim();
        return text.Length <= maxLength ? text : text[..maxLength] + "…";
    }

    /// <summary>根据 lego 输出行推进状态机阶段（仅影响界面文案与进度）。</summary>
    private static AcmeStage MapLogToStage(string line, bool isManual)
    {
        if (line.Contains("Wait for propagation", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("propagation", StringComparison.OrdinalIgnoreCase))
        {
            return AcmeStage.WaitingPropagation;
        }

        if (line.Contains("server validated", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("requesting certificates", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("certificate obtained", StringComparison.OrdinalIgnoreCase))
        {
            return AcmeStage.ObtainingCert;
        }

        if (line.Contains("cleaning up", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("challenge cleaned", StringComparison.OrdinalIgnoreCase))
        {
            return AcmeStage.CleaningUp;
        }

        return isManual ? AcmeStage.WaitingUserConfirm : AcmeStage.RunningPresent;
    }

    private static readonly Regex TxtRecordRegex = MyRegex();

    /// <summary>
    ///     从 lego 输出行中解析 TXT 记录主机名与值（手动模式用）。
    ///     典型输出：<c>lego: Please create ... : _acme-challenge.example.com. 120 IN TXT "xxx"</c>
    /// </summary>
    private static void ParseChallengeLine(string line, ref string? host, ref string? value)
    {
        try
        {
            var m = TxtRecordRegex.Match(line);
            if (!m.Success)
            {
                return;
            }

            value = m.Groups["value"].Value;
            var idx = line.IndexOf("_acme-challenge", StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                return;
            }

            var rest = line[idx..];
            var end = rest.IndexOf(' ');
            host = end > 0 ? rest[..end] : rest;
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Warning($"解析 lego 输出失败：{ex.Message}");
        }
    }

    [GeneratedRegex(
        @"_acme-challenge\S*\.\s*\d*\s*IN\s+TXT\s+""(?<value>[^""]+)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled, "zh-CN")]
    private static partial Regex MyRegex();
}
