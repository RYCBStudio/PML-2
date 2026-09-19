using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Core.Services;

namespace MEFrpLauncherX.Views;

/// <summary>
///     证书助手窗口（26.4）：为 HTTPS 隧道申请 SSL 证书。
///     当前实现<b>手动 DNS-01</b>：展示 lego 给出的 TXT 记录，
///     用户到 DNS 服务商添加后点「我已添加」，本窗口把确认写回 lego 进程。
/// </summary>
public partial class CertificateAssistantWindow : Window
{
    /// <summary>用于把「用户已添加 TXT」信号交回签发流程</summary>
    private TaskCompletionSource<bool>? _confirmTcs;

    /// <summary>当前签发任务的取消源</summary>
    private CancellationTokenSource? _cts;

    private bool _isIssuing;

    public CertificateAssistantWindow()
    {
        InitializeComponent();
    }

    private bool IsProduction => EnvBox.SelectedIndex == 1;

    private void EnvChanged(object? sender, SelectionChangedEventArgs e)
    {
        // 生产环境需显式提醒（限速风险），并在切换时提示成本
        ProductionWarning.IsOpen = IsProduction;
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

        // 生产环境二次确认，避免误触消耗签发配额
        if (IsProduction)
        {
            var cd = new FluentAvalonia.UI.Controls.ContentDialog
            {
                Title = Languages.Text_Certificate_Env_Production,
                Content = Languages.Text_Certificate_Env_ProductionWarning,
                PrimaryButtonText = Languages.Text_Global_Confirm,
                CloseButtonText = Languages.Text_Certificate_Cancel,
                DefaultButton = FluentAvalonia.UI.Controls.ContentDialogButton.Close
            };
            if (await cd.ShowAsync() != FluentAvalonia.UI.Controls.ContentDialogResult.Primary)
            {
                return;
            }
        }

        _isIssuing = true;
        StartButton.IsEnabled = false;
        ProgressExpander.IsVisible = true;
        ChallengePanel.IsVisible = false;
        ResultText.IsVisible = false;
        ConfirmButton.IsVisible = false;
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
            Staging = !IsProduction
        };

        try
        {
            var result = await AcmeCertificateService.IssueAsync(
                request,
                ConfirmChallengeAsync,
                OnProgress,
                _cts.Token);

            ResultText.IsVisible = true;
            if (result.Success)
            {
                ResultText.Text = string.Format(Languages.Text_Certificate_SuccessFormat,
                    result.CertificateDirectory);
                Growl.Success(Languages.Text_Certificate_Stage_Succeeded);
            }
            else
            {
                ResultText.Text = string.Format(Languages.Text_Certificate_FailedFormat,
                    result.Message ?? Languages.Text_Certificate_Stage_Failed);
                Growl.Error(Languages.Text_Certificate_Stage_Failed);
            }
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Error(ex, "证书申请异常");
            ResultText.IsVisible = true;
            ResultText.Text = string.Format(Languages.Text_Certificate_FailedFormat, ex.Message);
        }
        finally
        {
            _isIssuing = false;
            StartButton.IsEnabled = true;
            ConfirmButton.IsVisible = false;
            ChallengePanel.IsVisible = false;
            _confirmTcs = null;
            _cts?.Dispose();
            _cts = null;
        }
    }

    /// <summary>UI 线程更新进度条与阶段文案。</summary>
    private void OnProgress(AcmeProgress p)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ProgressBar.Value = p.Percent;
            StageText.Text = p.Message ?? StageText.Text;

            var hasChallenge = !string.IsNullOrWhiteSpace(p.ChallengeHost) ||
                               !string.IsNullOrWhiteSpace(p.ChallengeValue);
            if (hasChallenge)
            {
                ChallengeHostText.Text = p.ChallengeHost ?? string.Empty;
                ChallengeValueText.Text = p.ChallengeValue ?? string.Empty;
                ChallengePanel.IsVisible = true;
            }

            // 仅在等待用户确认时显示确认按钮
            ConfirmButton.IsVisible = p.Stage == AcmeStage.WaitingUserConfirm;
        });
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
        ConfirmButton.IsVisible = false;
        _confirmTcs?.TrySetResult(true);
    }

    private async void CopyChallengeHost(object? sender, RoutedEventArgs e) =>
        await CopyToClipboard(ChallengeHostText.Text);

    private async void CopyChallengeValue(object? sender, RoutedEventArgs e) =>
        await CopyToClipboard(ChallengeValueText.Text);

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
}
