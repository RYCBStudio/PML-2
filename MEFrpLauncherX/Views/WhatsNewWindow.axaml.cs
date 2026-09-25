using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Core.Models;

namespace MEFrpLauncherX.Views;

/// <summary>
///     「本次更新内容」窗口（26.4）。
///     应用升级后首次启动时展示当前版本的更新说明；版本未变化时不再弹出。
///     更新说明来自服务端 <c>changelog/latest</c>，因此老版本运行时也能取到最新条目；
///     若服务端版本与本地版本不一致或请求失败，则退化为「仅展示本地版本号」的提示，不阻塞启动。
/// </summary>
public partial class WhatsNewWindow : Window
{
    /// <summary>窗口展示的数据上下文（供 ItemsControl 绑定变更条目）。</summary>
    private readonly WhatsNewViewModel _vm = new();

    private static readonly HttpClient _http = new HttpClient();

    public WhatsNewWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        Opened += OnOpened;
    }

    ~WhatsNewWindow()
    {
        _http.Dispose();
    }

    /// <summary>
    ///     若当前版本尚未展示过更新内容则弹出窗口；返回是否实际展示。
    ///     全部异常在此吞掉（仅记日志），避免影响主窗口启动流程。
    /// </summary>
    /// <param name="owner">所属窗口（居中显示用）</param>
    public static async Task<bool> ShowIfNeededAsync(Window? owner)
    {
        try
        {
            if (!WhatsNewStateStore.ShouldShow(Core.App.Version))
            {
                return false;
            }

            var win = new WhatsNewWindow();
            if (owner is not null)
            {
                // 主窗口此时已显示，可作为 owner 居中显示
                await win.ShowDialog(owner);
            }
            else
            {
                win.Show();
            }

            // 只要展示过（用户关闭与否）即记录，避免下次启动重复弹出
            WhatsNewStateStore.MarkShown(Core.App.Version);
            return true;
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Error(ex, "展示更新内容窗口失败");
            return false;
        }
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        HeaderText.Text = string.Format(Languages.Text_WhatsNew_HeaderFormat,
            Core.App.Version, App.Codename, DateTime.Now.ToString("yyyy-MM-dd"));
        PlaceholderText.Text = string.Format(Languages.Text_WhatsNew_LoadingFormat, Core.App.Version);
        LoadingRing.IsVisible = true;
        ChangeList.IsVisible = false;

        await LoadChangelogAsync();
    }

    /// <summary>
    ///     拉取服务端最新更新说明并填充列表。
    ///     AOT 提示：只使用已登记在 JsonSerializerContext 的类型（SingleVersionInfo）。
    /// </summary>
    private async Task LoadChangelogAsync()
    {
        try
        {
            var info = await RYCBApiConverter.GetLatestVersionInfoAsync();
            var changes = info?.Data?.Changes;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                LoadingRing.IsVisible = false;

                if (info is not { Success: true } || changes is null || changes.Length == 0)
                {
                    PlaceholderText.Text = Languages.Text_WhatsNew_Unavailable;
                    PlaceholderText.IsVisible = true;
                    ChangeList.IsVisible = false;
                    return;
                }

                _vm.Changelog.Clear();
                foreach (var line in changes)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        _vm.Changelog.Add(line);
                    }
                }

                SummaryText.Text = string.IsNullOrWhiteSpace(info.Data?.Description)
                    ? string.Empty
                    : info.Data!.Description;
                PlaceholderText.IsVisible = false;
                ChangeList.IsVisible = true;
            });
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Error(ex, "加载更新说明失败");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                LoadingRing.IsVisible = false;
                PlaceholderText.Text = Languages.Text_WhatsNew_Unavailable;
                PlaceholderText.IsVisible = true;
                ChangeList.IsVisible = false;
            });
        }
    }

    private void CloseWindow(object? sender, RoutedEventArgs e) => Close();

    private void OpenChangelog(object? sender, RoutedEventArgs e)
    {
        // 关闭本窗口并导航到更新页，便于用户查看完整历史
        Close();
        ViewModels.MainPageFrameViewModel.Instance?.NavigateToPage("Update");
    }

    private async void SwitchSource(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is TabStrip tab)
        {
            // tab.SelectedIndex = tab.SelectedIndex == 0 ? 1 : 0;
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/120.0 Safari/537.36");
            var url = $"https://blog.pml2.rycb.tech/changelog/{Core.App.Version}.md";
#if DEBUG
            url = "https://blog.pml2.rycb.tech/changelog/26.3.0.md";
#endif
            string post;
            try
            {
                post = await _http.GetStringAsync(url);
            }
            catch (Exception exception)
            {
                Core.App.CurrentLogger?.Error(exception, "Error fetching post");
                Growl.Error(Languages.Text_Update_FetchFailedTip);
                return;
            }
            post = FrontMatter().Replace(post, "");
            if (tab.SelectedIndex == 0)
            {
                BlogPresenter.Value = post;
                ApiGrid.Hide();
                BlogPresenter.Show();
            }
            else
            {
                BlogPresenter.Hide();
                ApiGrid.Show();
            }
        }
    }

    [GeneratedRegex(@"\A---\s*\r?\n.*?\r?\n---\s*\r?\n", RegexOptions.Singleline
    )]
    private static partial Regex FrontMatter();
}

/// <summary>「本次更新内容」窗口的视图模型：仅承载变更条目列表。</summary>
public class WhatsNewViewModel
{
    /// <summary>变更条目（服务端 changes 原文，支持 HTML/Markdown）</summary>
    public Avalonia.Collections.AvaloniaList<string> Changelog
    {
        get;
    } = [];
}