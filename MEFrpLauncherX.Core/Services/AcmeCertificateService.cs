using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using MEFrpLauncherX.Core.Models;

namespace MEFrpLauncherX.Core.Services;

/// <summary>证书申请阶段（供 UI 展示进度与提示）</summary>
public enum AcmeStage
{
    /// <summary>下载 lego</summary>
    DownloadingLego,

    /// <summary>正在下单并等待挑战信息</summary>
    Preparing,

    /// <summary>已拿到 TXT 记录，等待用户到 DNS 添加后确认</summary>
    WaitingUserConfirm,

    /// <summary>等待 DNS 传播与服务端校验</summary>
    WaitingValidation,

    /// <summary>签发成功</summary>
    Succeeded,

    /// <summary>失败</summary>
    Failed
}

/// <summary>一次证书申请的进度回调载荷</summary>
/// <param name="Stage">当前阶段</param>
/// <param name="Percent">总体进度（0–100）</param>
/// <param name="ChallengeHost">TXT 记录主机名（形如 <c>_acme-challenge.example.com.</c>）</param>
/// <param name="ChallengeValue">TXT 记录值</param>
/// <param name="Message">可读状态文案</param>
public sealed record AcmeProgress(
    AcmeStage Stage,
    double Percent,
    string? ChallengeHost = null,
    string? ChallengeValue = null,
    string? Message = null);

/// <summary>证书申请请求</summary>
public sealed record AcmeRequest
{
    /// <summary>主域名（如 <c>example.com</c>）</summary>
    public required string Domain { get; init; }

    /// <summary>附加域名 / SAN（可空）</summary>
    public IReadOnlyList<string> AltNames { get; init; } = [];

    /// <summary>ACME 账户邮箱</summary>
    public required string Email { get; init; }

    /// <summary>是否使用 Staging 环境（默认 true，避免触发生产环境限速）</summary>
    public bool Staging { get; init; } = true;
}

/// <summary>证书申请结果</summary>
public sealed record AcmeResult(bool Success, string? CertificateDirectory, string? Message);

/// <summary>
///     基于 lego 子进程的 ACME 证书签发服务（26.4）。
///     仅实现<b>手动 DNS-01</b>：lego 输出需要添加的 TXT 记录并等待回车，
///     由 UI 展示给用户、用户到 DNS 服务商添加后点确认，本服务把回车写入 stdin。
///     设计约束：
///     <list type="bullet">
///         <item>不引入托管 ACME 库，避免 AOT/裁剪风险（lego 为独立 Go 二进制）；</item>
///         <item>命令参数以 <c>argv</c> 数组传递，未经 shell，路径含空格/中文安全；</item>
///         <item>传播等待超时调大（默认 60s 偏短，手动添加 TXT 常来不及）。</item>
///     </list>
/// </summary>
public static class AcmeCertificateService
{
    /// <summary>Let's Encrypt 生产目录</summary>
    public const string LetsEncryptProduction = "https://acme-v02.api.letsencrypt.org/directory";

    /// <summary>Let's Encrypt 测试目录（默认使用，签发速度快且不消耗生产配额）</summary>
    public const string LetsEncryptStaging = "https://acme-staging-v02.api.letsencrypt.org/directory";

    /// <summary>DNS 传播等待上限（秒）；手动添加 TXT 需要时间，故显著大于 lego 默认的 60s</summary>
    private const int PropagationTimeoutSeconds = 300;

    private static string WorkRoot
    {
        get;
    } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "lego");

    /// <summary>lego 的工作目录（账户密钥与证书都落在这里，避免污染用户配置目录）</summary>
    public static string WorkPath => Path.Combine(WorkRoot, "accounts");

    /// <summary>
    ///     交互式签发证书。调用方需通过 <paramref name="confirmChallenge" /> 在
    ///     用户确认「已添加 TXT 记录」后返回（返回 false 表示用户取消）。
    /// </summary>
    /// <param name="request">签发请求</param>
    /// <param name="confirmChallenge">
    ///     确认回调：收到 TXT 记录信息后被调用，返回 true 表示继续（写回车给 lego）。
    /// </param>
    /// <param name="progress">进度回调</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>
    ///     成功时 <c>CertificateDirectory</c> 为证书目录（含 fullchain.pem / privkey.pem / meta.json）。
    /// </returns>
    public static async Task<AcmeResult> IssueAsync(
        AcmeRequest request,
        Func<AcmeProgress, Task<bool>> confirmChallenge,
        Action<AcmeProgress>? progress = null,
        CancellationToken ct = default)
    {
        // 1. 确保 lego 可用
        progress?.Invoke(new AcmeProgress(AcmeStage.DownloadingLego, 2, Message: "正在准备 lego…"));
        var legoPath = await LegoDownloader.EnsureAsync(
            (percent, message) => progress?.Invoke(
                new AcmeProgress(AcmeStage.DownloadingLego, percent * 0.3, Message: message)),
            ct);
        if (legoPath is null)
        {
            return new AcmeResult(false, null, "无法准备 lego（下载或校验失败），请检查网络后重试。");
        }

        // 2. 组装命令（全部作为独立 argv 项，不经 shell）
        var args = new List<string>
        {
            "run",
            "--accept-tos",
            "--dns", "manual",
            "--email", request.Email,
            "--server", request.Staging ? LetsEncryptStaging : LetsEncryptProduction,
            "--path", WorkPath,
            "-d", request.Domain
        };
        foreach (var alt in request.AltNames.Where(a => !string.IsNullOrWhiteSpace(a)))
        {
            args.Add("-d");
            args.Add(alt.Trim());
        }

        var psi = new ProcessStartInfo
        {
            FileName = legoPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            WorkingDirectory = Path.GetDirectoryName(legoPath) ?? WorkRoot,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        // 手动 DNS 场景需要更长的传播等待
        psi.EnvironmentVariables["MANUAL_PROPAGATION_TIMEOUT"] = PropagationTimeoutSeconds.ToString();
        psi.EnvironmentVariables["MANUAL_POLLING_INTERVAL"] = "5";

        progress?.Invoke(new AcmeProgress(AcmeStage.Preparing, 32, Message: "正在向 CA 下单…"));

        try
        {
            using var process = new Process { StartInfo = psi };
            var stdoutSeen = new StringBuilder();

            // 记录最近一次解析到的挑战信息，供确认回调使用
            var challengeHost = (string?)null;
            var challengeValue = (string?)null;
            var confirmTask = (Task<bool>?)null;

            process.OutputDataReceived += (_, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data))
                {
                    return;
                }

                stdoutSeen.AppendLine(e.Data);
                ParseChallengeLine(e.Data, ref challengeHost, ref challengeValue);

                // 「Press 'Enter' when you are done」出现即进入等待用户确认阶段
                if (e.Data.Contains("Press 'Enter'", StringComparison.OrdinalIgnoreCase) &&
                    confirmTask is null)
                {
                    var snapshot = new AcmeProgress(AcmeStage.WaitingUserConfirm, 55, challengeHost,
                        challengeValue, "请在 DNS 服务商添加 TXT 记录后继续");
                    progress?.Invoke(snapshot);
                    confirmTask = HandleConfirmAsync(process, snapshot, confirmChallenge, progress);
                }
                else if (e.Data.Contains("Wait for propagation", StringComparison.OrdinalIgnoreCase))
                {
                    progress?.Invoke(new AcmeProgress(AcmeStage.WaitingValidation, 75, challengeHost,
                        challengeValue, "正在等待 DNS 生效…"));
                }
                else if (e.Data.Contains("server validated", StringComparison.OrdinalIgnoreCase) ||
                         e.Data.Contains("requesting certificates", StringComparison.OrdinalIgnoreCase))
                {
                    progress?.Invoke(new AcmeProgress(AcmeStage.WaitingValidation, 88, challengeHost,
                        challengeValue, "校验通过，正在签发…"));
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            // 必须同时消费 stderr，避免管道缓冲写满导致 lego 阻塞
            var stderrTask = process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct);

            // 等待确认流程收尾（若已触发）
            if (confirmTask is not null)
            {
                var confirmed = await confirmTask;
                if (!confirmed)
                {
                    return new AcmeResult(false, null, "已取消证书申请。");
                }
            }

            var stderr = await stderrTask;
            if (process.ExitCode != 0)
            {
                App.CurrentLogger?.Warning($"lego 退出码 {process.ExitCode}：{stderr}");
                return new AcmeResult(false, null,
                    $"证书申请失败（lego 退出码 {process.ExitCode}）。常见原因：DNS 记录未生效、域名不正确或 CA 限速。");
            }

            // 3. 整理产物：lego 的 {domain}.crt 已含 CA 链，直接重命名为 fullchain.pem
            progress?.Invoke(new AcmeProgress(AcmeStage.Succeeded, 95, challengeHost, challengeValue,
                "正在整理证书文件…"));
            var legoCertsDir = Path.Combine(WorkPath, "certificates");
            var meta = new CertificateMeta
            {
                Domain = request.Domain,
                AltNames = request.AltNames.ToList(),
                Staging = request.Staging,
                IssuedAt = DateTimeOffset.UtcNow,
                AcmeServer = request.Staging ? LetsEncryptStaging : LetsEncryptProduction,
                LegoVersion = LegoDownloader.LegoVersion
            };

            var dir = CertStore.SaveFromLego(legoCertsDir, request.Domain, meta);
            if (dir is null)
            {
                return new AcmeResult(false, null, "证书已签发但文件整理失败，请查看日志。");
            }

            progress?.Invoke(new AcmeProgress(AcmeStage.Succeeded, 100, challengeHost, challengeValue, "签发完成"));
            return new AcmeResult(true, dir, null);
        }
        catch (OperationCanceledException)
        {
            return new AcmeResult(false, null, "已取消证书申请。");
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Error(ex, "证书申请失败");
            return new AcmeResult(false, null, $"证书申请失败：{ex.Message}");
        }
    }

    /// <summary>
    ///     等待用户确认后向 lego 写入回车。取消或异常时返回 false。
    /// </summary>
    private static async Task<bool> HandleConfirmAsync(
        Process process,
        AcmeProgress snapshot,
        Func<AcmeProgress, Task<bool>> confirmChallenge,
        Action<AcmeProgress>? progress)
    {
        try
        {
            var ok = await confirmChallenge(snapshot);
            if (!ok)
            {
                // 用户取消：终止 lego，避免其继续等待
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(true);
                    }
                }
                catch
                {
                    // 进程可能已退出
                }

                return false;
            }

            await process.StandardInput.WriteLineAsync();
            await process.StandardInput.FlushAsync();
            progress?.Invoke(new AcmeProgress(AcmeStage.WaitingValidation, 70, snapshot.ChallengeHost,
                snapshot.ChallengeValue, "已提交，正在等待 CA 校验…"));
            return true;
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Error(ex, "确认 DNS 挑战失败");
            return false;
        }
    }

    private static readonly Regex TxtRecordRegex = new(
        @"_acme-challenge\S*\.\s*\d*\s*IN\s+TXT\s+""(?<value>[^""]+)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    ///     从 lego 输出行中解析 TXT 记录主机名与值。
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
            if (idx >= 0)
            {
                var rest = line[idx..];
                var end = rest.IndexOf(' ');
                host = end > 0 ? rest[..end] : rest;
            }
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Warning($"解析 lego 输出失败：{ex.Message}");
        }
    }
}
