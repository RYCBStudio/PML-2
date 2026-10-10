using System;
using System.Threading.Tasks;
using FluentAvalonia.UI.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Core.Models;
using MEFrpLauncherX.Core.Services;

namespace MEFrpLauncherX.Controls;

/// <summary>
///     「多余版本目录」启动提醒（26.5.0）。
///     <para>
///         分离式布局下升级会保留旧版本目录作为回滚点，这是刻意设计；但历次升级累积后
///         安装根可能堆到 2~3 份代码，磁盘吃紧。因此在「用户已允许」的前提下，
///         当可回收空间达到阈值时于启动流程里提醒一次。
///     </para>
///     <para>
///         <b>不打扰的保证</b>：
///     </para>
///     <list type="bullet">
///         <item>用户可在「更新 → 旧版本目录 → 提醒清理多余版本」里彻底关闭；</item>
///         <item>同一提醒至少间隔 7 天，且可回收体积需有明显增长（见 <see cref="VersionCleanupStateStore" />）；</item>
///         <item>用户选择「暂不处理」同样记一次提醒，不会每次启动都弹；</item>
///         <item>整个流程异常内部吞掉，绝不影响启动。</item>
///     </list>
/// </summary>
public static class VersionCleanupReminder
{
    /// <summary>
    ///     在启动流程中按需提醒用户清理多余版本目录。
    ///     <para>无论是否提醒、用户如何选择，都不会抛出异常。</para>
    /// </summary>
    public static async Task ShowIfNeededAsync()
    {
        try
        {
            // 用户显式关闭提醒 —— 不再打扰
            if (!ConfigManager.CurrentConfig.UpdateSettings.NotifyRedundantVersion)
            {
                return;
            }

            // 非版本化布局（开发态 / ≤26.4）没有版本目录概念
            if (!AppPaths.IsVersionedLayout)
            {
                return;
            }

            var inspection = VersionCleanupService.Inspect();

            // 阈值判断与节流判断
            if (!VersionCleanupService.ShouldNotify(inspection)
                || !VersionCleanupStateStore.ShouldNotify(inspection.RemovableSizeBytes))
            {
                return;
            }

            var dialog = new FAContentDialog
            {
                Title = Languages.Text_Update_VersionNotify_Title,
                Content = string.Format(Languages.Text_Update_VersionNotify_Message,
                    inspection.Removable.Count,
                    ToolboxService.FormatFileSize(inspection.RemovableSizeBytes)),
                PrimaryButtonText = Languages.Text_Update_CleanupVersionsNow,
                CloseButtonText = Languages.Text_Global_Later,
                DefaultButton = FAContentDialogButton.Close
            };

            // 无论用户选「立即清理」还是「暂不处理」，都记一次提醒，避免下次启动再弹
            var result = await dialog.ShowAsync();

            VersionCleanupStateStore.MarkNotified(inspection.RemovableSizeBytes);

            if (result == FAContentDialogResult.Primary)
            {
                await CleanupAsync();
            }
        }
        catch (Exception ex)
        {
            // 提醒是「锦上添花」，任何失败都不该影响启动。
            // 注意 fully-qualified：Avalonia 也有 App 类型，必须用 Core.App。
            Core.App.CurrentLogger?.Error(ex, "提醒清理多余版本目录失败");
        }
    }

    /// <summary>
    ///     二次确认后删除可清理的版本目录，并给出结果提示。
    /// </summary>
    private static async Task CleanupAsync()
    {
        // 弹窗期间用户可能已在别处清理过，重新巡检以免对着空列表操作
        var inspection = VersionCleanupService.Inspect();
        if (inspection.Removable.Count == 0)
        {
            Growl.Info(Languages.Text_Update_VersionCleanup_NothingToDo);
            return;
        }

        var confirm = new FAContentDialog
        {
            Title = Languages.Text_Update_VersionCleanup_ConfirmTitle,
            Content = string.Format(Languages.Text_Update_VersionCleanup_ConfirmMessage,
                inspection.Removable.Count,
                ToolboxService.FormatFileSize(inspection.RemovableSizeBytes)),
            PrimaryButtonText = Languages.Text_Update_CleanupVersionsNow,
            CloseButtonText = Languages.Text_Global_Cancel,
            DefaultButton = FAContentDialogButton.Close
        };

        if (await confirm.ShowAsync() != FAContentDialogResult.Primary)
        {
            return;
        }

        var (removed, failed, freed) = VersionCleanupService.RemoveAllRemovable();

        if (removed > 0)
        {
            Growl.Success(string.Format(Languages.Text_Update_VersionCleanup_Done, removed,
                ToolboxService.FormatFileSize(freed)));
        }

        if (failed > 0)
        {
            Growl.Warning(Languages.Text_Update_VersionCleanup_Failed);
        }
    }
}