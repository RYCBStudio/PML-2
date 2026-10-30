using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Core.Models;
using MEFrpLauncherX.Core.Services;

namespace MEFrpLauncherX.Controls;

/// <summary>
///     「旧版残留收拢 + 提示删除」（26.5.1）。
///     <para>
///         旧版（≤26.4）升级过来后，安装根里会留下 <c>Resources\</c>、<c>runtimes\</c>、
///         <c>Tools\</c>、<c>bin\</c> 等扁平残留，以及迁移向导归档出的
///         <c>*.old-yyyyMMdd_HHmmss\</c>。它们不属于当前布局，会让安装目录看起来「畸形」。
///     </para>
///     <para>
///         启动流程里做两件事：
///         <list type="number">
///             <item>静默把残留搬进 <c>legacy\</c>（<see cref="LegacyLayoutCleaner.Sweep" />，幂等）；</item>
///             <item>提示用户确认删除 —— 只搬不移是刻意的，删除必须由用户点头。</item>
///         </list>
///     </para>
///     <para>
///         <b>不打扰的保证</b>：提醒间隔见 <see cref="LegacyLeftoverState.RemindInterval" />（7 天）；
///         整个流程异常内部吞掉，绝不影响启动。
///     </para>
/// </summary>
public static class LegacyLeftoverReminder
{
    /// <summary>
    ///     在启动流程中收拢旧版残留并按需提示删除。
    ///     <para>无论是否搬移、用户如何选择，都不会抛出异常。</para>
    /// </summary>
    public static async Task ShowIfNeededAsync()
    {
        try
        {
            // 开发态 / ≤26.4 扁平布局的安装根本身就是代码目录，不能动
            if (!AppPaths.IsVersionedLayout)
            {
                return;
            }

            // 1. 静默收拢（幂等：没有残留时直接返回）
            var sweep = await Task.Run(() => LegacyLayoutCleaner.Sweep(Core.App.CurrentLogger));
            if (sweep.Failed.Count > 0)
            {
                // 搬不动的（被占用）只记日志，不打断启动，下次启动会再试
                Core.App.CurrentLogger?.Warning(
                    $"有 {sweep.Failed.Count} 项旧版残留未能移入 {AppPaths.LegacyFolderName}\\，下次启动将重试",
                    module: EnumLogModule.Update);
            }

            // 2. 统计 legacy 现状（含历史上已收拢但用户还没删的内容）
            var leftovers = LegacyLayoutCleaner.InspectLegacy();
            if (leftovers.Count == 0)
            {
                return;
            }

            var bytes = leftovers.Sum(item => item.SizeBytes);
            if (!LegacyLeftoverStateStore.ShouldNotify(bytes))
            {
                return;
            }

            // 3. 提示用户删除（只搬不移，所以这里只问「删不删」）
            var names = string.Join("、", leftovers.Take(6).Select(item => item.Name));
            if (leftovers.Count > 6)
            {
                names += " …";
            }

            var dialog = new FAContentDialog
            {
                Title = Languages.Text_Legacy_Title,
                Content = BuildContent(leftovers.Count, bytes, names),
                PrimaryButtonText = Languages.Text_Legacy_DeleteNow,
                SecondaryButtonText = Languages.Text_Legacy_OpenFolder,
                CloseButtonText = Languages.Text_Global_Later,
                DefaultButton = FAContentDialogButton.Close
            };

            var result = await dialog.ShowAsync();

            // 无论用户选了什么，都记一次提醒，7 天内不再打扰
            LegacyLeftoverStateStore.MarkNotified(bytes);

            switch (result)
            {
                case FAContentDialogResult.Primary:
                    DeleteLegacy();
                    break;
                case FAContentDialogResult.Secondary:
                    OpenFolder(AppPaths.LegacyDirectory);
                    break;
            }
        }
        catch (Exception ex)
        {
            // 收拢与提醒都是「锦上添花」，任何失败都不该影响启动。
            Core.App.CurrentLogger?.Error(ex, "整理旧版残留失败");
        }
    }

    /// <summary>弹窗正文：数量 + 体积 + 条目示例 + 实际路径（可换行，避免长文本撑破对话框）。</summary>
    private static Control BuildContent(int count, long bytes, string names) => new StackPanel
    {
        Spacing = 8,
        Width = 440,
        Children =
        {
            new TextBlock
            {
                Text = string.Format(Languages.Text_Legacy_Message, count, ToolboxService.FormatFileSize(bytes), names),
                TextWrapping = TextWrapping.Wrap
            },
            new TextBlock
            {
                Text = string.Format(Languages.Text_Legacy_MessagePath, AppPaths.LegacyDirectory),
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.8,
                Classes = { "CaptionTextBlock" }
            }
        }
    };

    /// <summary>删除 <c>legacy\</c> 中全部旧版残留并提示结果。</summary>
    private static void DeleteLegacy()
    {
        var (failed, freed) = LegacyLayoutCleaner.DeleteLegacy(Core.App.CurrentLogger);

        if (freed > 0 || failed == 0)
        {
            Growl.Success(string.Format(Languages.Text_Legacy_Deleted, ToolboxService.FormatFileSize(freed)));
        }

        if (failed > 0)
        {
            Growl.Warning(Languages.Text_Legacy_DeleteFailed);
        }
    }

    /// <summary>在系统文件管理器中打开指定文件夹（跨平台）。</summary>
    private static void OpenFolder(string folderPath)
    {
        try
        {
            Directory.CreateDirectory(folderPath);

            var (fileName, arguments) = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? ("explorer", $"\"{folderPath}\"")
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? ("open", $"\"{folderPath}\"")
                    : ("xdg-open", $"\"{folderPath}\"");

            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Error(ex, "打开 legacy 目录失败");
        }
    }
}
