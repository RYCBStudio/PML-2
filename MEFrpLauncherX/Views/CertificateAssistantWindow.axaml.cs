using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
// Avalonia 12：IClipboard.SetTextAsync 改为扩展方法（ClipboardExtensions）。
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using FluentAvalonia.MarkdownRender.Helper;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Core.Models;
using MEFrpLauncherX.Core.Services;
using MEFrpLauncherX.Core.Storage;

namespace MEFrpLauncherX.Views;

/// <summary>
///     证书助手窗口（26.4）。
///     支持两种验证方式：
///     <list type="bullet">
///         <item><b>DNS 账户</b>（阶段 B，默认）：选好账户后一键完成
///               Present → 校验 → 出证 → CleanUp，无需手改解析；</item>
///         <item><b>手动 DNS</b>（阶段 A）：展示 lego 给出的 TXT 记录，
///               用户到 DNS 服务商添加后点「我已添加」继续。</item>
///     </list>
///     界面展示的 lego 输出已由 <see cref="SecretRedactor" /> 脱敏。
/// </summary>
public partial class CertificateAssistantWindow : Window
{
    private const int MaxLogLines = 300;

    private readonly StringBuilder _logBuffer = new();

    /// <summary>用于把「用户已添加 TXT」信号交回签发流程（手动模式）</summary>
    private TaskCompletionSource<bool>? _confirmTcs;

    /// <summary>当前签发任务的取消源</summary>
    private CancellationTokenSource? _cts;

    /// <summary>本次申请成功后生成的证书（用于「用于隧道」与复制路径）</summary>
    private CertificateListItem? _issued;

    private bool _isIssuing;

    public CertificateAssistantWindow()
    {
        InitializeComponent();
        EmailBox.Text = UserCache.CurrentUser?.Email;
        // 26.4：显式给出传播等待上限初值（与 AcmeCertificateService 默认值同源）
        PropagationTimeoutBox.Value = AcmeCertificateService.DefaultPropagationTimeoutSeconds;
        ReloadDnsAccounts();
        UpdateModeUi();
        ReloadLocalCertificates();
    }

    private bool IsProduction => EnvBox?.SelectedIndex == 1;

    private bool IsDnsAccountMode => ModeBox?.SelectedIndex == 0;

    /// <summary>重新载入 DNS 账户下拉（仅展示摘要，不含凭据）。</summary>
    private void ReloadDnsAccounts()
    {
        var items = DnsAccountStore.List();
        DnsAccountBox.ItemsSource = items;
        DnsAccountBox.SelectedIndex = items.Count > 0 ? 0 : -1;
        UpdateModeUi();
    }

    /// <summary>按当前验证方式切换 DNS 账户区域的可用性与提示。</summary>
    private void UpdateModeUi()
    {
        var dnsMode = IsDnsAccountMode;
        DnsAccountItem?.IsVisible = dnsMode;

        if (!dnsMode)
        {
            NoDnsAccountHint?.IsOpen = false;
            return;
        }

        NoDnsAccountHint?.IsOpen = DnsAccountBox.ItemCount == 0;
    }

    private void EnvChanged(object? sender, SelectionChangedEventArgs e)
    {
        // 生产环境需显式提醒（限速风险），并在切换时提示成本
        ProductionWarning?.IsOpen = IsProduction;
    }

    private void ModeChanged(object? sender, SelectionChangedEventArgs e) => UpdateModeUi();

    /// <summary>打开 DNS 账户管理窗口；关闭后刷新下拉。</summary>
    private async void ManageDnsAccounts(object? sender, RoutedEventArgs e)
    {
        await new DnsAccountsWindow().ShowDialog(Core.App.MainWindow);
        ReloadDnsAccounts();
    }

    private async void StartIssue(object? sender, RoutedEventArgs e)
    {
        if (_isIssuing)
        {
            return;
        }

        var domain = DomainBox.Text?.Trim();
        var email = EmailBox.Text?.Trim();

        if (string.IsNullOrWhiteSpace(domain))
        {
            StageText.Text = Languages.Text_Certificate_Validation_DomainRequired;
            return;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            StageText.Text = Languages.Text_Certificate_Validation_EmailRequired;
            return;
        }

        if (!email.Contains('@') || !email.Contains('.'))
        {
            StageText.Text = Languages.Text_Certificate_Validation_EmailInvalid;
            return;
        }

        var dnsMode = IsDnsAccountMode;
        DnsAccountSummary? account = null;
        if (dnsMode)
        {
            account = DnsAccountBox.SelectedItem as DnsAccountSummary;
            if (account is null)
            {
                StageText.Text = Languages.Text_Certificate_Validation_DnsAccountRequired;
                return;
            }
        }

        // 生产环境二次确认，避免误触消耗签发配额
        if (IsProduction)
        {
            var cd = new FluentAvalonia.UI.Controls.FAContentDialog
            {
                Title = Languages.Text_Certificate_Env_Production,
                Content = Languages.Text_Certificate_Env_ProductionWarning,
                PrimaryButtonText = Languages.Text_Global_Confirm,
                CloseButtonText = Languages.Text_Certificate_Cancel,
                DefaultButton = FluentAvalonia.UI.Controls.FAContentDialogButton.Close
            };
            if (await cd.ShowAsync() != FluentAvalonia.UI.Controls.FAContentDialogResult.Primary)
            {
                return;
            }
        }

        _isIssuing = true;
        _issued = null;
        StartButton?.IsEnabled = false;
        ProgressExpander.IsVisible = true;
        ChallengePanel?.IsVisible = false;
        ResultText?.IsVisible = false;
        LogPanel?.IsVisible = true;
        CopyPathButton?.IsVisible = false;
        UseForTunnelButton?.IsVisible = false;
        ConfirmButton?.IsVisible = false;
        _logBuffer.Clear();
        LogBox.Text = string.Empty;
        _cts = new CancellationTokenSource();

        var altNames = (AltNamesBox.Text ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .ToList();

        var request = new AcmeRequest
        {
            Domain = domain!,
            Email = email!,
            AltNames = altNames,
            Staging = !IsProduction,
            Mode = dnsMode ? AcmeChallengeMode.DnsAccount : AcmeChallengeMode.Manual,
            DnsAccountId = account?.Id,
            DnsAccountDisplayName = account?.DisplayName,
            PropagationTimeoutSeconds = ReadPropagationTimeout(),
            SkipPropagationCheck = SkipPropagationCheck.IsChecked == true
        };

        try
        {
            var result = await AcmeCertificateService.IssueAsync(
                request,
                dnsMode ? null : ConfirmChallengeAsync,
                OnProgress,
                _cts.Token);

            ResultText?.IsVisible = true;
            if (result.Success)
            {
                ResultText.Text = string.Format(Languages.Text_Certificate_SuccessFormat,
                    result.CertificateDirectory);
                Growl.Success(Languages.Text_Certificate_Stage_Succeeded);

                // 记录产物，供「用于隧道」与复制路径使用
                _issued = CertStore.List().FirstOrDefault(c =>
                    string.Equals(c.FullChainPath,
                        Path.Combine(result.CertificateDirectory!, "fullchain.pem"),
                        StringComparison.OrdinalIgnoreCase));

                CopyPathButton?.IsVisible = _issued is not null;
                UseForTunnelButton?.IsVisible = _issued is not null;

                // 26.4：新证书已落盘，同步刷新「本地证书」列表
                ReloadLocalCertificates();
            }
            else
            {
                ResultText.Text = string.Format(Languages.Text_Certificate_FailedFormat, result.Message);
                ProgressBar.Foreground =
                    Application.Current.TryGetResource("SystemFillColorCriticalBrush", Application.Current.ActualThemeVariant,
                        out var o1)
                        ? o1 as IBrush
                        : null;
                Growl.Error(Languages.Text_Certificate_Stage_Failed);
            }
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Error(ex, "证书申请异常");
            ResultText?.IsVisible = true;
            ResultText.Text = string.Format(Languages.Text_Certificate_FailedFormat, ex.Message);
        }
        finally
        {
            _isIssuing = false;
            StartButton?.IsEnabled = true;
            ConfirmButton?.IsVisible = false;
            ChallengePanel?.IsVisible = false;
            _confirmTcs = null;
            _cts?.Dispose();
            _cts = null;
        }
    }

    /// <summary>UI 线程更新进度条、阶段文案与脱敏日志。</summary>
    private void OnProgress(AcmeProgress p)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ProgressBar.Value = p.Percent;

            var stageText = StageTextFor(p.Stage);
            if (!string.IsNullOrWhiteSpace(stageText))
            {
                StageText.Text = stageText;
            }
            else if (!string.IsNullOrWhiteSpace(p.Message))
            {
                StageText.Text = p.Message;
            }

            var hasChallenge = !string.IsNullOrWhiteSpace(p.ChallengeHost) ||
                               !string.IsNullOrWhiteSpace(p.ChallengeValue);
            if (hasChallenge)
            {
                ChallengeHostText.Text = p.ChallengeHost ?? string.Empty;
                ChallengeValueText.Text = p.ChallengeValue ?? string.Empty;
                ChallengePanel?.IsVisible = true;
            }

            // 仅在等待用户确认时显示确认按钮（手动模式）
            ConfirmButton?.IsVisible = p.Stage == AcmeStage.WaitingUserConfirm;

            if (!string.IsNullOrWhiteSpace(p.Log))
            {
                AppendLog(p.Log);
            }
        });
    }

    /// <summary>把状态机阶段映射为界面文案（未定义文案时返回空串，由调用方回退到 Message）。</summary>
    private static string StageTextFor(AcmeStage stage) => stage switch
    {
        AcmeStage.CheckingLego => Languages.Text_Certificate_Stage_DownloadingLego,
        AcmeStage.RunningPresent => Languages.Text_Certificate_Stage_RunningPresent,
        AcmeStage.WaitingUserConfirm => Languages.Text_Certificate_Stage_WaitingUserConfirm,
        AcmeStage.WaitingPropagation => Languages.Text_Certificate_Stage_WaitingPropagation,
        AcmeStage.ObtainingCert => Languages.Text_Certificate_Stage_ObtainingCert,
        AcmeStage.CleaningUp => Languages.Text_Certificate_Stage_CleaningUp,
        AcmeStage.Succeeded => Languages.Text_Certificate_Stage_Succeeded,
        AcmeStage.Failed => Languages.Text_Certificate_Stage_Failed,
        _ => string.Empty
    };

    /// <summary>追加一行脱敏日志（限制行数，避免长时间运行后界面卡顿）。</summary>
    private void AppendLog(string line)
    {
        var lines = _logBuffer.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length >= MaxLogLines)
        {
            _logBuffer.Clear();
            // 保留最近一半，形成滑动窗口
            var keep = lines.Skip(lines.Length / 2).ToArray();
            foreach (var l in keep)
            {
                _logBuffer.AppendLine(l.TrimEnd('\r'));
            }
        }

        _logBuffer.AppendLine(line);
        LogBox.Text = _logBuffer.ToString();
        LogBox.CaretIndex = LogBox.Text?.Length ?? 0;
    }

    /// <summary>
    ///     等待用户在窗口上点击「我已添加」；返回 false 表示用户取消（终止 lego）。
    /// </summary>
    private Task<bool> ConfirmChallengeAsync(AcmeProgress snapshot)
    {
        _confirmTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        return _confirmTcs.Task;
    }

    private void ConfirmChallenge(object? sender, RoutedEventArgs e)
    {
        ConfirmButton?.IsVisible = false;
        _confirmTcs?.TrySetResult(true);
    }

    private async void CopyChallengeHost(object? sender, RoutedEventArgs e) =>
        await CopyToClipboard(ChallengeHostText.Text);

    private async void CopyChallengeValue(object? sender, RoutedEventArgs e) =>
        await CopyToClipboard(ChallengeValueText.Text);

    /// <summary>复制已签发证书的绝对路径，便于粘贴到隧道配置。</summary>
    private async void CopyCertificatePath(object? sender, RoutedEventArgs e)
    {
        if (_issued is null)
        {
            return;
        }

        var text = $"{_issued.FullChainPath}{Environment.NewLine}{_issued.PrivateKeyPath}";
        await CopyToClipboard(text);
    }

    /// <summary>提示如何在隧道中使用该证书（隧道表单已提供「从证书助手选择」）。</summary>
    private void UseForTunnel(object? sender, RoutedEventArgs e)
    {
        if (_issued is null)
        {
            return;
        }

        Growl.Info(Languages.Text_Certificate_UseForTunnelHint);
    }

    private async Task CopyToClipboard(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        try
        {
            var clipboard = Core.App.MainWindow?.Clipboard;
            if (clipboard is not null)
            {
                await clipboard.SetTextAsync(text);
                Growl.Success(Languages.Text_ConfigPreviewer_CopiedToClipboard);
            }
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Error(ex, "复制到剪贴板失败");
        }
    }

    private void OpenDirectory(object? sender, RoutedEventArgs e)
    {
        try
        {
            var dir = CertStore.RootPath;
            Directory.CreateDirectory(dir);
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", dir);
            }
            else
            {
                Process.Start("xdg-open", dir);
            }
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Error(ex, "打开证书目录失败");
        }
    }

    /// <summary>关闭窗口；若仍在申请中则先取消（避免遗留 lego 进程）。</summary>
    private void CloseWindow(object? sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_isIssuing)
        {
            try
            {
                _cts?.Cancel();
                // 通知签发流程用户已取消，触发 lego 进程终止
                _confirmTcs?.TrySetResult(false);
            }
            catch (Exception ex)
            {
                Core.App.CurrentLogger?.Error(ex, "取消证书申请失败");
            }
        }

        base.OnClosing(e);
    }

    /// <summary>读取界面上的传播等待上限（无有效输入时回退到默认 300 秒，由 Core 再次收敛）。</summary>
    private int ReadPropagationTimeout()
    {
        var value = PropagationTimeoutBox.Value;
        return double.IsNaN(value)
            ? AcmeCertificateService.DefaultPropagationTimeoutSeconds
            : (int)value;
    }

    /// <summary>重新枚举本地证书并重建列表（无证书时显示空态）。</summary>
    private void ReloadLocalCertificates()
    {
        try
        {
            LocalCertList.Children.Clear();
            var items = CertStore.List();
            LocalCertEmptyText.IsVisible = items.Count == 0;

            foreach (var item in items)
            {
                LocalCertList.Children.Add(BuildLocalCertificateRow(item));
            }
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Error(ex, "加载本地证书列表失败");
        }
    }

    /// <summary>
    ///     构建一行本地证书：域名 / 附加域名 / 证书路径 + 「删除」按钮。
    ///     行内容在代码中构建（不走 XAML 绑定），以避免 AOT 下的反射绑定风险。
    /// </summary>
    private Control BuildLocalCertificateRow(CertificateListItem item)
    {
        var tertiary = (IBrush?)Application.Current.FindResource(Application.Current.ActualThemeVariant, "TextFillColorTertiaryBrush") ?? Brushes.Gray;

        var info = new StackPanel { Spacing = 2 };
        info.Children.Add(new TextBlock { Text = item.DisplayName, FontWeight = FontWeight.SemiBold });
        if (!string.IsNullOrWhiteSpace(item.AltNamesText))
        {
            info.Children.Add(new TextBlock { Text = item.AltNamesText, FontSize = 12, Foreground = tertiary });
        }

        if (item.IsExpiringSoon)
        {
            info.Children.Add(new TextBlock
            {
                Text = Languages.Text_Certificate_ExpiringSoon,
                FontSize = 12,
                Foreground = tertiary
            });
        }

        info.Children.Add(new TextBlock
        {
            Text = item.FullChainPath,
            FontSize = 11,
            TextTrimming = TextTrimming.LeadingCharacterEllipsis,
            Foreground = tertiary
        });

        var delete = new Button
        {
            Content = Languages.Text_Certificate_Delete,
            Tag = item,
            VerticalAlignment = VerticalAlignment.Center
        };
        delete.Click += DeleteLocalCertificate;

        var row = new Grid { ColumnDefinitions = [with("*,Auto")] };
        Grid.SetColumn(info, 0);
        Grid.SetColumn(delete, 1);
        row.Children.Add(info);
        row.Children.Add(delete);
        return row;
    }

    /// <summary>
    ///     删除本地证书（二次确认后删除整个证书目录）。
    ///     删除不影响已在使用该证书的隧道；仅影响后续「从证书助手选择」。
    /// </summary>
    private async void DeleteLocalCertificate(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: CertificateListItem item })
        {
            return;
        }

        var confirm = new FluentAvalonia.UI.Controls.FAContentDialog
        {
            Title = Languages.Text_Certificate_Delete,
            Content = string.Format(Languages.Text_Certificate_DeleteConfirmFormat, item.DisplayName),
            PrimaryButtonText = Languages.Text_Global_Confirm,
            CloseButtonText = Languages.Text_Global_Cancel,
            DefaultButton = FluentAvalonia.UI.Controls.FAContentDialogButton.Close
        };
        if (await confirm.ShowAsync() != FluentAvalonia.UI.Controls.FAContentDialogResult.Primary)
        {
            return;
        }

        if (CertStore.Delete(item.Slug))
        {
            Growl.Success(Languages.Text_Certificate_Deleted);
            ReloadLocalCertificates();
        }
        else
        {
            Growl.Error(Languages.Text_Global_Failed);
        }
    }

    private void ViewDocuments(object? sender, RoutedEventArgs e)
    {
        UrlHelper.OpenUrl("https://docs.rycb.tech/pml-2/cert");
    }
}