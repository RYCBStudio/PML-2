using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls;
using FluentAvalonia.MarkdownRender.Controls.MarkdownRender;
using FluentAvalonia.UI.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Core.Models;
using ReactiveUI;
// ReactiveUI 25：无参 ReactiveCommand 的载体类型 RxVoid（替代 System.Reactive.Unit）位于此命名空间。
using ReactiveUI.Primitives;
using ReactiveUI.Primitives;

namespace MEFrpLauncherX.ViewModels;

/// <summary>
///     收件箱（<c>InboxViewer</c>）的视图模型（26.4）。
///     <para>
///         两类通知：<b>系统通知</b>（MEFrp <c>auth/notice</c> 返回的整段 markdown）与
///         <b>软件公告</b>（RYCB <c>notice</c> 接口返回的公告列表，条目为 <see cref="NoticeContent" />）。
///     </para>
///     <para>
///         「是否有新内容」走<b>本地缓存快照</b>（<see cref="InboxNoticeStateStore" />，落
///         <c>Cache/inbox-notice.json</c>）：把当前解析结果与上一次快照做差集，
///         只把「这次新出现」的部分标记为新；快照写入为<b>状态替换</b>而非合并，
///         保证公告被撤回后不会残留。
///         快照仅在用户真正查看收件箱时（<see cref="MarkAsRead" />）写回，
///         因此未读的新内容会持续通过徽标提示，而不会在加载时被静默「已读」。
///     </para>
///     <para>
///         本 VM 不发起任何网络请求：数据由主页加载流程（<c>HomePageViewModel.LoadUserDataAsync</c>）
///         统一获取后注入，既避免重复请求，也复用既有的 5 分钟缓存。
///     </para>
/// </summary>
public class InboxViewModel : ViewModelBase
{
    /// <summary>
    ///     公告切块规则：按行首的 markdown 标题（<c>#</c>~<c>######</c>）或分隔线（<c>---</c>/<c>***</c>/<c>___</c>）切分，
    ///     得到「一条公告」的原始字符串列表。
    /// </summary>
    private static readonly Regex AnnouncementSplitter = new(
        @"\r?\n[ \t]*(?:-{3,}|\*{3,}|_{3,})[ \t]*\r?\n|\r?\n(?=#{1,6}[ \t])",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    /// <summary>markdown 语法清理（生成纯文本摘要用），带超时避免异常输入拖死 UI 线程</summary>
    private static readonly Regex MarkdownImage =
        new(@"!\[[^\]]*\]\([^)]*\)", RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    private static readonly Regex MarkdownLink =
        new(@"\[([^\]]*)\]\([^)]*\)", RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    private static readonly Regex MarkdownBlockMarker =
        new(@"^[ \t]{0,3}(?:#{1,6}|>|[-*+]|\d+\.)[ \t]*", RegexOptions.Multiline | RegexOptions.Compiled,
            TimeSpan.FromSeconds(2));

    private static readonly Regex MarkdownEmphasis =
        new(@"(\*\*|__|\*|_|`)", RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    private static readonly Regex Whitespace =
        new(@"\s+", RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    /// <summary>本次解析出的系统公告条目（快照写回时使用）</summary>
    private List<string> _currentAnnouncements = [];

    /// <summary>本次拉取到的软件公告 ID（快照写回时使用）</summary>
    private List<int> _currentSoftwareIds = [];

    /// <summary>本次系统通知是否成功取到（false 表示请求失败/尚未加载，此时不应写回该来源的快照）</summary>
    private bool _systemNoticeLoaded;

    /// <summary>本次软件公告是否成功取到（同上）</summary>
    private bool _softwareNoticeLoaded;

    public InboxViewModel()
    {
        ShowSystemNoticeDetailCommand = ReactiveCommand.CreateFromTask(ShowSystemNoticeDetailAsync);

        if (Design.IsDesignMode)
        {
            Load(DesignModeSystemNotice, DesignModeSoftwareNotices);
        }
    }

    /// <param name="systemNoticeMarkdown">系统通知的完整 markdown；为空表示暂无系统通知</param>
    /// <param name="softwareNotices">软件公告条目；为 null 或空表示暂无软件公告</param>
    public InboxViewModel(string? systemNoticeMarkdown, IEnumerable<NoticeContent>? softwareNotices)
    {
        ShowSystemNoticeDetailCommand = ReactiveCommand.CreateFromTask(ShowSystemNoticeDetailAsync);
        Load(systemNoticeMarkdown, softwareNotices);
    }

    // ==================== 系统通知 ====================

    /// <summary>系统通知标题（详情对话框标题）</summary>
    public string SystemNoticeUpdateTitle => Languages.Text_Main_PlatformNotice_Title;

    /// <summary>系统通知全文 markdown（仅用于「查看详情」对话框，不在列表中内联渲染）</summary>
    public string? SystemNoticeUpdateContent
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    ///     系统通知的纯文本摘要（列表内联展示，配合 <c>TextTrimming</c> 截断）。
    ///     markdown 语法会被剥离，避免半截语法符号出现在截断文本里。
    /// </summary>
    public string? SystemNoticePreview
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>本次新出现的系统公告条数（本地缓存快照差集）</summary>
    public int NewSystemNoticeCount
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>系统通知是否有新内容（徽标显示依据）</summary>
    public bool IsSystemNoticeUpdateAvailable => NewSystemNoticeCount > 0;

    /// <summary>是否有系统通知内容可展示（无内容时显示空态）</summary>
    public bool HasSystemNotice => !string.IsNullOrWhiteSpace(SystemNoticePreview);

    /// <summary>是否有可查看详情的系统通知</summary>
    public bool CanShowSystemNoticeDetail => !string.IsNullOrWhiteSpace(SystemNoticeUpdateContent);

    /// <summary>是否显示系统通知空态</summary>
    public bool IsSystemNoticeEmpty => !HasSystemNotice;

    /// <summary>打开系统通知详情（markdown 全文渲染）</summary>
    public ReactiveCommand<RxVoid, RxVoid> ShowSystemNoticeDetailCommand
    {
        get;
    }

    // ==================== 软件公告 ====================

    /// <summary>软件公告条目（保持接口返回顺序）</summary>
    public AvaloniaList<InboxSoftwareNoticeItem> SoftwareNotices
    {
        get;
    } = [];

    /// <summary>是否有软件公告（供空态切换）</summary>
    public bool HasSoftwareNotices => SoftwareNotices.Count > 0;

    /// <summary>是否显示软件公告空态</summary>
    public bool IsSoftwareNoticeEmpty => !HasSoftwareNotices;

    /// <summary>本次新出现的软件公告条数（本地缓存快照差集）</summary>
    public int NewSoftwareNoticeCount
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>软件公告是否有新内容（徽标显示依据）</summary>
    public bool IsSoftwareNoticeUpdateAvailable => NewSoftwareNoticeCount > 0;

    /// <summary>两类通知中任意一类有新内容（主页收件箱按钮徽标依据）</summary>
    public bool HasNewNotice => IsSystemNoticeUpdateAvailable || IsSoftwareNoticeUpdateAvailable;

    /// <summary>
    ///     用最新数据重建收件箱内容，并按本地缓存快照计算「新内容」标记。
    ///     <b>注意</b>：这里只计算、不落盘；快照在 <see cref="MarkAsRead" /> 中写回。
    /// </summary>
    /// <param name="systemNoticeMarkdown">系统通知 markdown；null 表示无内容</param>
    /// <param name="softwareNotices">软件公告条目；null 表示无内容</param>
    /// <param name="systemNoticeFetched">
    ///     系统通知是否成功取到。false 表示请求失败/尚未加载，此时该来源快照不参与写回，
    ///     避免把历史记录误清空后旧公告被重新判为「新」。
    /// </param>
    /// <param name="softwareNoticesFetched">软件公告是否成功取到（同上）</param>
    public void Load(string? systemNoticeMarkdown, IEnumerable<NoticeContent>? softwareNotices,
        bool systemNoticeFetched = true, bool softwareNoticesFetched = true)
    {
        _systemNoticeLoaded = systemNoticeFetched;
        _softwareNoticeLoaded = softwareNoticesFetched;
        LoadSystemNotice(systemNoticeMarkdown);
        LoadSoftwareNotices(softwareNotices is null ? null : [.. softwareNotices]);

        this.RaisePropertyChanged(nameof(HasNewNotice));
        this.RaisePropertyChanged(nameof(HasSoftwareNotices));
        this.RaisePropertyChanged(nameof(IsSoftwareNoticeEmpty));
    }

    private void LoadSystemNotice(string? systemNoticeMarkdown)
    {
        var current = ParseAnnouncements(systemNoticeMarkdown);
        _currentAnnouncements = current;
        SystemNoticeUpdateContent = systemNoticeMarkdown;

        // 只算「这次新出现」的：当前条目 差集 上一次快照
        var previous = InboxNoticeStateStore.LoadSystemNoticeSnapshot();
        var added = current.Except(previous, StringComparer.Ordinal).ToList();
        NewSystemNoticeCount = added.Count;

        // 有新内容时优先展示新条目，便于用户直接看到变化
        var primary = added.Count > 0 ? added[0] : current.FirstOrDefault();
        var preview = ToPlainText(primary);
        SystemNoticePreview = !string.IsNullOrWhiteSpace(preview)
            ? preview
            : string.IsNullOrWhiteSpace(systemNoticeMarkdown)
                ? null
                // 纯图片类公告：正文没有可提取文字，给出提示文案，避免出现空白卡片
                : Languages.Text_Inbox_NoTextPreview;

        this.RaisePropertyChanged(nameof(IsSystemNoticeUpdateAvailable));
        this.RaisePropertyChanged(nameof(HasSystemNotice));
        this.RaisePropertyChanged(nameof(IsSystemNoticeEmpty));
        this.RaisePropertyChanged(nameof(CanShowSystemNoticeDetail));
    }

    private void LoadSoftwareNotices(IReadOnlyList<NoticeContent>? softwareNotices)
    {
        var previousIds = InboxNoticeStateStore.LoadSoftwareNoticeIds();
        var currentIds = new List<int>();

        SoftwareNotices.Clear();
        if (softwareNotices is not null)
        {
            foreach (var notice in softwareNotices)
            {
                currentIds.Add(notice.Id);
                SoftwareNotices.Add(new InboxSoftwareNoticeItem(notice, !previousIds.Contains(notice.Id)));
            }
        }

        _currentSoftwareIds = currentIds;
        NewSoftwareNoticeCount = currentIds.Except(previousIds).Count();
        this.RaisePropertyChanged(nameof(IsSoftwareNoticeUpdateAvailable));
    }

    /// <summary>
    ///     标记为已读：把本次内容快照<b>整体替换</b>写回本地缓存（不是合并），
    ///     之后同样的公告不会再被判为新内容。用户实际打开收件箱时调用。
    ///     <para>
    ///         仅对<b>本次确实取到数据</b>的来源写回；请求失败的来源保持旧快照不动，
    ///         避免失败（数据为空）被误当成「公告已被全部撤回」而清空历史记录。
    ///     </para>
    /// </summary>
    public void MarkAsRead()
    {
        try
        {
            // 1) 写回本地快照（状态替换语义）
            if (_systemNoticeLoaded)
            {
                InboxNoticeStateStore.SaveSystemNoticeSnapshot(_currentAnnouncements);
            }

            if (_softwareNoticeLoaded)
            {
                InboxNoticeStateStore.SaveSoftwareNoticeIds(_currentSoftwareIds);
            }

            // 2) 同步清零「新内容」标记，使红点立即消失（无需等待下次加载）
            if (_systemNoticeLoaded)
            {
                NewSystemNoticeCount = 0;
            }

            if (_softwareNoticeLoaded)
            {
                NewSoftwareNoticeCount = 0;
                foreach (var item in SoftwareNotices)
                {
                    item.IsNew = false;
                }
            }

            this.RaisePropertyChanged(nameof(IsSystemNoticeUpdateAvailable));
            this.RaisePropertyChanged(nameof(IsSoftwareNoticeUpdateAvailable));
            this.RaisePropertyChanged(nameof(HasNewNotice));
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Error(ex, "标记收件箱已读失败");
        }
    }

    /// <summary>
    ///     把 markdown 公告按标题 / 分隔线切成「一条公告」的列表。
    ///     单条公告内容不含切分标记本身，便于逐条做快照差集。
    /// </summary>
    public static List<string> ParseAnnouncements(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return [];
        }

        try
        {
            return
            [
                .. AnnouncementSplitter
                    .Split(markdown)
                    .Select(block => block.Trim())
                    .Where(block => !string.IsNullOrWhiteSpace(block))
            ];
        }
        catch (RegexMatchTimeoutException)
        {
            // 切分超时（异常输入）时退化为「整段算一条」，保证功能可用
            return [markdown.Trim()];
        }
    }

    /// <summary>把 markdown 片段转为单行纯文本摘要（供 TextTrimming 展示）。</summary>
    public static string ToPlainText(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        try
        {
            var text = MarkdownImage.Replace(markdown, " ");
            text = MarkdownLink.Replace(text, "$1");
            text = MarkdownBlockMarker.Replace(text, " ");
            text = MarkdownEmphasis.Replace(text, string.Empty);
            return Whitespace.Replace(text, " ").Trim();
        }
        catch (RegexMatchTimeoutException)
        {
            return markdown.Trim();
        }
    }

    /// <summary>
    ///     打开系统通知详情：markdown 全文渲染（列表内只展示截断摘要）。
    ///     详情对话框自身独立于主窗口（沿用 <see cref="NoticeContent" /> 详情的既有做法）。
    /// </summary>
    private async Task ShowSystemNoticeDetailAsync()
    {
        if (!CanShowSystemNoticeDetail)
        {
            return;
        }

        try
        {
            var dialog = new FAContentDialog
            {
                Title = SystemNoticeUpdateTitle,
                Content = new MarkdownRender
                {
                    Value = SystemNoticeUpdateContent,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    MaxHeight = 420,
                    MinWidth = 320
                },
                PrimaryButtonText = Languages.Text_Global_Confirm,
                CloseButtonText = Languages.Text_Global_Close,
                DefaultButton = FAContentDialogButton.Primary
            };

            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Error(ex, "打开系统通知详情失败");
        }
    }

    /// <summary>设计时示例数据（仅用于 XAML 设计器预览）</summary>
    private static string DesignModeSystemNotice => """
                                                    ## 进群须知
                                                    请自行搜索验证答案，进群申请使用机器人审核，无效答案会被自动拒绝。图片中答案即为**反面例子**。

                                                    ![A347AEC597614E71F67B215283BFDAD1.png](https://img.fastmirror.net/s/2026/07/12/6a53379a52d1e.png)
                                                    """;

    private static IReadOnlyList<NoticeContent> DesignModeSoftwareNotices =>
    [
        new()
        {
            Id = 1,
            Type = Languages.Text_AppearanceSettings_SoftwareNotice,
            Summary = "测试公告",
            Date = "2026-09-30",
            ContentOfNotice = "*这*是一条**测试**公告。",
            Active = true,
            Priority = 1
        }
    ];
}

/// <summary>
///     收件箱内的软件公告行（26.4）。
///     对 <see cref="NoticeContent" /> 的只读包装：补充「是否新内容」标记与纯文本摘要，
///     并复用 Core 既有的 <see cref="NoticeContent.ShowNoticeCommand" /> 打开详情，
///     保证与经典主页的公告详情走完全同一路径。
/// </summary>
public sealed class InboxSoftwareNoticeItem : ReactiveObject
{
    public InboxSoftwareNoticeItem(NoticeContent notice, bool isNew)
    {
        Notice = notice;
        IsNew = isNew;
    }

    /// <summary>源公告对象（详情命令由它提供）</summary>
    public NoticeContent Notice
    {
        get;
    }

    /// <summary>公告 ID</summary>
    public int Id => Notice.Id;

    /// <summary>公告标题（接口 summary 字段，缺失时回退为日期）</summary>
    public string Title => string.IsNullOrWhiteSpace(Notice.Summary) ? Notice.Date : Notice.Summary;

    /// <summary>公告日期</summary>
    public string Date => Notice.Date;

    /// <summary>公告类型（已由转换器本地化）</summary>
    public string Type => Notice.Type;

    /// <summary>纯文本摘要（列表内联展示，配合 TextTrimming 截断）</summary>
    public string Preview
    {
        get
        {
            var text = InboxViewModel.ToPlainText(Notice.ContentOfNotice);
            return string.IsNullOrWhiteSpace(text) ? Languages.Text_Inbox_NoTextPreview : text;
        }
    }

    /// <summary>是否为本次新出现的公告（本地缓存快照差集结果）</summary>
    public bool IsNew
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>打开详情：复用 Core 中 <see cref="NoticeContent" /> 的既有对话框命令。</summary>
    public ReactiveCommand<RxVoid, RxVoid> ShowNoticeCommand => Notice.ShowNoticeCommand;
}