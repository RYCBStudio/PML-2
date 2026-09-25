using System.Diagnostics;
using System.Text;

namespace MEFrpLauncherX.Core.Services;

/// <summary>lego 的运行方式（26.4 阶段 B 新增 DNS 账号自动模式）。</summary>
public enum LegoChallengeMode
{
    /// <summary>DNS 账号自动验证（本阶段新增，一键 Present/校验/出证/CleanUp）</summary>
    DnsApi,

    /// <summary>手动 DNS（阶段 A，保留）</summary>
    Manual
}

/// <summary>一次 lego 调用的请求描述。</summary>
public sealed record LegoRunRequest
{
    /// <summary>验证方式</summary>
    public required LegoChallengeMode Mode
    {
        get;
        init;
    }

    /// <summary>主域名</summary>
    public required string Domain
    {
        get;
        init;
    }

    /// <summary>附加域名 / SAN（可含通配符 <c>*.example.com</c>）</summary>
    public IReadOnlyList<string> AltNames
    {
        get;
        init;
    } = [];

    /// <summary>ACME 账户邮箱</summary>
    public required string Email
    {
        get;
        init;
    }

    /// <summary>是否使用 Staging 环境</summary>
    public bool Staging
    {
        get;
        init;
    } = true;

    /// <summary>lego 的 <c>--dns</c> provider 代码（手动模式为 <c>manual</c>）</summary>
    public string LegoProvider
    {
        get;
        init;
    } = "manual";

    /// <summary>服务商凭据对应的环境变量（<b>只在子进程内生效，永不落日志</b>）</summary>
    public IReadOnlyDictionary<string, string>? ProviderEnvironment
    {
        get;
        init;
    }

    /// <summary>服务商环境变量前缀，用于附加传播等待参数</summary>
    public string ProviderEnvPrefix
    {
        get;
        init;
    } = string.Empty;

    /// <summary>lego 工作目录（账户密钥与证书都落在这里）</summary>
    public required string WorkPath
    {
        get;
        init;
    }

    /// <summary>DNS 传播等待上限（秒）；DNS API 模式下通过 <c>{PREFIX}_PROPAGATION_TIMEOUT</c> 生效</summary>
    public int PropagationTimeoutSeconds
    {
        get;
        init;
    } = 300;

    /// <summary>
    ///     高级选项：跳过 lego 的传播检查（默认关闭）。
    ///     lego v5 中通过 <c>--dns.propagation.wait &lt;duration&gt;</c> 实现——该 flag 会
    ///     <b>关闭全部传播检查</b>并改为固定等待，因此只在用户明确选择时传入；
    ///     固定等待时长见 <see cref="LegoRunner.SkipPropagationWaitSeconds" />。
    /// </summary>
    public bool SkipPropagationCheck
    {
        get;
        init;
    }
}

/// <summary>lego 调用的最终结果。</summary>
/// <param name="Success">是否成功（退出码为 0）</param>
/// <param name="ExitCode">进程退出码</param>
/// <param name="StandardOutput">已脱敏的标准输出</param>
/// <param name="StandardError">已脱敏的标准错误</param>
/// <param name="CertificatesDirectory">lego 的产物目录（<c>{path}/certificates</c>）</param>
/// <param name="Error">已分类的错误信息（成功时为 null）</param>
public sealed record LegoRunResult(
    bool Success,
    int ExitCode,
    string StandardOutput,
    string StandardError,
    string CertificatesDirectory,
    AcmeErrorInfo? Error = null);

/// <summary>
///     lego 子进程运行器（26.4 阶段 B）。
///     设计约束：
///     <list type="bullet">
///         <item>lego 永远以<b>独立子进程</b>运行，不链入 AOT 主程序；</item>
///         <item>命令参数以 <c>ArgumentList</c> 传递，不经 shell，路径含空格/中文安全；</item>
///         <item>凭据只通过 <c>EnvironmentVariables</c> 传入子进程，不写命令行（避免出现在进程列表）；
///               回收输出时统一走 <see cref="SecretRedactor" />。</item>
///     </list>
/// </summary>
public static class LegoRunner
{
    /// <summary>手动模式专用：lego 读取的传播等待上限环境变量</summary>
    private const string ManualPropagationTimeoutEnv = "MANUAL_PROPAGATION_TIMEOUT";

    /// <summary>手动模式专用：lego 读取的轮询间隔环境变量</summary>
    private const string ManualPollingIntervalEnv = "MANUAL_POLLING_INTERVAL";

    /// <summary>
    ///     选择「跳过传播检查」时的固定等待时长（秒）。
    ///     对应 <c>--dns.propagation.wait 30s</c>：不轮询权威/递归 DNS，
    ///     只等固定时间，故给出一个较保守的值，尽量避免 CA 校验过快失败。
    /// </summary>
    public const int SkipPropagationWaitSeconds = 30;

    /// <summary>
    ///     组装 lego 的 argv。
    ///     易踩的两个坑（均已在真实 lego v5 上验证）：
    ///     <list type="number">
    ///         <item>v5 起全局 flag 必须放在子命令 <c>run</c> <b>之后</b>；</item>
    ///         <item><c>--dns.propagation.wait</c> 的类型是 <b>duration</b>（Go 语法，如 <c>30s</c>），
    ///               传裸数字 <c>300</c> 会报 <c>missing unit in duration</c>；并且该 flag 的语义是
    ///               「<b>关闭全部传播检查</b>、改为固定等待」，<b>不是</b>传播超时上限——
    ///               正常模式下传播超时由厂商环境变量 <c>{PREFIX}_PROPAGATION_TIMEOUT</c> 控制，
    ///               因此平时<b>不能</b>传此 flag。</item>
    ///     </list>
    /// </summary>
    internal static List<string> BuildArguments(LegoRunRequest request) => BuildArgumentsCore(request);

    /// <summary>实际生成 argv（抽出为独立方法，便于自检直接校验参数形态）。</summary>
    private static List<string> BuildArgumentsCore(LegoRunRequest request)
    {
        var args = new List<string>
        {
            "run",
            "--accept-tos",
            "--dns", request.LegoProvider,
            "--email", request.Email,
            "--server", request.Staging
                ? AcmeCertificateService.LetsEncryptStaging
                : AcmeCertificateService.LetsEncryptProduction,
            "--path", request.WorkPath,
            "--dns.resolvers", "119.29.29.29,223.5.5.5",
            "--dns.propagation-timeout", "300s",
        };

        // 仅在用户明确选择「跳过传播检查」时才传入；正常模式不传，保留 lego 的真实传播检查。
        if (request.SkipPropagationCheck)
        {
            args.Add("--dns.propagation-disable-ans");
            args.Add("--dns.propagation.wait");
            args.Add(FormatDuration(SkipPropagationWaitSeconds));
        }

        // 主域名与 SAN 都通过 -d 传入；含通配符时 lego 会强制走 DNS-01
        args.Add("-d");
        args.Add(request.Domain);
        foreach (var alt in request.AltNames)
        {
            if (string.IsNullOrWhiteSpace(alt))
            {
                continue;
            }

            args.Add("-d");
            args.Add(alt.Trim());
        }

        return args;
    }

    /// <summary>
    ///     把秒数格式化为 lego（Go）可解析的 duration 字面量，例如 <c>30s</c> / <c>5m0s</c>。
    ///     Go duration 必须带单位，否则会报 <c>missing unit in duration</c>。
    /// </summary>
    internal static string FormatDuration(int seconds)
    {
        var total = Math.Max(0, seconds);
        var minutes = total / 60;
        var remainder = total % 60;

        if (minutes == 0)
        {
            return $"{remainder}s";
        }

        return remainder == 0 ? $"{minutes}m0s" : $"{minutes}m{remainder}s";
    }

    /// <summary>
    ///     本次调用需要注入子进程的环境变量（含凭据与传播参数）。
    ///     返回值<b>不得</b>写入日志或界面。
    /// </summary>
    internal static Dictionary<string, string> BuildEnvironment(LegoRunRequest request)
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal);

        if (request.Mode == LegoChallengeMode.Manual)
        {
            env[ManualPropagationTimeoutEnv] = request.PropagationTimeoutSeconds.ToString();
            env[ManualPollingIntervalEnv] = "5";
            return env;
        }

        if (request.ProviderEnvironment is not null)
        {
            foreach (var (key, value) in request.ProviderEnvironment)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    env[key] = value;
                }
            }
        }

        // 按厂商前缀覆盖传播等待（各 provider 的默认值普遍偏短）
        if (!string.IsNullOrWhiteSpace(request.ProviderEnvPrefix))
        {
            env[$"{request.ProviderEnvPrefix}_PROPAGATION_TIMEOUT"] =
                request.PropagationTimeoutSeconds.ToString();
            env[$"{request.ProviderEnvPrefix}_POLLING_INTERVAL"] = "5";
        }

        return env;
    }

    /// <summary>本次调用涉及的全部敏感值（用于输出脱敏）。</summary>
    private static List<string> CollectSecrets(LegoRunRequest request)
    {
        var secrets = new List<string>();
        if (request.ProviderEnvironment is not null)
        {
            secrets.AddRange(request.ProviderEnvironment.Values.Where(v => !string.IsNullOrWhiteSpace(v)));
        }

        return secrets;
    }

    /// <summary>确保 lego 可用（首次使用会按 RID 下载并校验 SHA-256）。</summary>
    public static Task<string?> EnsureLegoAsync(Action<double, string>? progress = null,
        CancellationToken ct = default) => LegoDownloader.EnsureAsync(progress, ct);

    /// <summary>
    ///     执行一次 lego 调用。所有输出都会先脱敏再交给回调，凭据不会离开子进程环境。
    /// </summary>
    /// <param name="legoPath">lego 可执行文件路径（由 <see cref="EnsureLegoAsync" /> 获得）</param>
    /// <param name="request">调用参数</param>
    /// <param name="onOutput">实时输出回调（已脱敏，逐行）</param>
    /// <param name="onManualConfirmRequested">
    ///     手动模式专用：lego 提示「请添加 TXT 记录」时被调用；
    ///     返回 true 表示已添加（写入回车继续），false 表示用户取消。
    /// </param>
    /// <param name="ct">取消令牌</param>
    public static async Task<LegoRunResult> RunAsync(
        string legoPath,
        LegoRunRequest request,
        Action<string>? onOutput = null,
        Func<Task<bool>>? onManualConfirmRequested = null,
        CancellationToken ct = default)
    {
        var certificatesDir = Path.Combine(request.WorkPath, "certificates");
        var secrets = CollectSecrets(request);

        var psi = new ProcessStartInfo
        {
            FileName = legoPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // 手动模式需要向 lego 的 stdin 写入回车以确认已添加 TXT
            RedirectStandardInput = request.Mode == LegoChallengeMode.Manual,
            WorkingDirectory = Path.GetDirectoryName(legoPath) ?? request.WorkPath,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var a in BuildArguments(request))
        {
            psi.ArgumentList.Add(a);
        }

        foreach (var (key, value) in BuildEnvironment(request))
        {
            psi.EnvironmentVariables[key] = value;
        }

        var stdout = new StringBuilder();
        var stderrBuffer = new StringBuilder();
        Task<bool>? confirmTask = null;

        try
        {
            using var process = new Process();
            process.StartInfo = psi;

            process.OutputDataReceived += (_, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data))
                {
                    return;
                }

                var line = SecretRedactor.Redact(e.Data, secrets);
                stdout.AppendLine(line);
                onOutput?.Invoke(line);

                // 手动模式：lego 打印该提示后即阻塞等待回车
                if (request.Mode == LegoChallengeMode.Manual &&
                    onManualConfirmRequested is not null &&
                    confirmTask is null &&
                    e.Data.Contains("Press 'Enter'", StringComparison.OrdinalIgnoreCase))
                {
                    confirmTask = ConfirmManualAsync(process, onManualConfirmRequested);
                }
            };

            // stderr 同样需要实时消费，避免管道写满阻塞 lego
            process.ErrorDataReceived += (_, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data))
                {
                    return;
                }

                var line = SecretRedactor.Redact(e.Data, secrets);
                stderrBuffer.AppendLine(line);
                onOutput?.Invoke(line);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(ct);

            var stderr = stderrBuffer.ToString();
            if (confirmTask is not null)
            {
                var confirmed = await confirmTask;
                if (!confirmed)
                {
                    return new LegoRunResult(false, process.ExitCode, stdout.ToString(), stderr,
                        certificatesDir, new AcmeErrorInfo(AcmeErrorKind.Unknown, "Text.Dns.Error.Cancelled"));
                }
            }

            if (process.ExitCode != 0)
            {
                var error = AcmeErrorMapper.Map(stderr, stdout.ToString());
                return new LegoRunResult(false, process.ExitCode, stdout.ToString(), stderr, certificatesDir, error);
            }

            return new LegoRunResult(true, 0, stdout.ToString(), stderr, certificatesDir);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Error(ex, "运行 lego 失败");
            return new LegoRunResult(false, -1, stdout.ToString(), SecretRedactor.Redact(ex.Message, secrets),
                certificatesDir, new AcmeErrorInfo(AcmeErrorKind.LegoUnavailable, "Text.Dns.Error.LegoUnavailable"));
        }
    }

    /// <summary>等待用户确认后向 lego 写入回车；取消则终止进程。</summary>
    private static async Task<bool> ConfirmManualAsync(Process process, Func<Task<bool>> confirm)
    {
        try
        {
            var ok = await confirm();
            if (!ok)
            {
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
            return true;
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Error(ex, "确认 DNS 挑战失败");
            return false;
        }
    }
}