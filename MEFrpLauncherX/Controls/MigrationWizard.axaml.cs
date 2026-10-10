using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Services;

namespace MEFrpLauncherX.Controls;

/// <summary>
///     26.5.0 数据迁移向导。
///     <para>
///         在「工具箱」中手动打开，或由 <see cref="ShouldPromptOnStartup" /> 判定为首次升级时自动弹出。
///         实际的搬迁工作由 <see cref="LegacyLayoutMigrator" /> 完成，本控件只负责展示与交互。
///     </para>
/// </summary>
public partial class MigrationWizard : UserControl
{
    public MigrationWizard()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshPlan();
    }

    // ---------- 启动时提示的判定 ----------

    /// <summary>标记文件：记录用户是否已被询问过迁移。</summary>
    private static string PromptMarker => Path.Combine(AppPaths.CacheDirectory, "migration-prompted.json");

    /// <summary>
    ///     是否应在本次启动时自动弹出迁移向导。
    ///     <para>条件：处于版本化发布布局、确实存在旧版数据、且此前未询问过。</para>
    /// </summary>
    public static bool ShouldPromptOnStartup()
    {
        if (!AppPaths.IsVersionedLayout)
        {
            // 开发态无需打扰。
            return false;
        }

        try
        {
            if (File.Exists(PromptMarker))
            {
                return false;
            }

            return LegacyLayoutMigrator.BuildPlan(AppPaths.InstallRoot, AppPaths.DataRoot).Count > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>记录「已询问过」，避免每次启动都弹窗。</summary>
    public static void MarkPrompted()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.CacheDirectory);
            File.WriteAllText(PromptMarker, DateTimeOffset.UtcNow.ToString());
        }
        catch
        {
            // 标记失败只会导致下次再问一次，无害。
        }
    }

    /// <summary>清除标记，使下次启动重新询问（供「重新运行迁移向导」使用）。</summary>
    public static void ResetPrompt()
    {
        try
        {
            if (File.Exists(PromptMarker))
            {
                File.Delete(PromptMarker);
            }
        }
        catch
        {
            // 同上，无害。
        }
    }

    // ---------- 清单展示 ----------

    private void RefreshPlan()
    {
        var plan = LegacyLayoutMigrator.BuildPlan(AppPaths.InstallRoot, AppPaths.DataRoot);

        PlanList.ItemsSource = plan.Select(item => new MigrationPlanRow(
            "📁",
            item.Label,
            $"{Shorten(item.Source)}  →  {Shorten(item.Target)}",
            FormatSize(item.Source))).ToList();

        EmptyHint.IsVisible = plan.Count == 0;
        MigrateButton.IsEnabled = plan.Count > 0;
    }

    // ---------- 操作 ----------

    private async void Migrate_Click(object? sender, RoutedEventArgs e)
    {
        if (!MigrateButton.IsEnabled)
        {
            return;
        }

        MigrateButton.IsEnabled = false;
        BusyRing.IsVisible = true;

        try
        {
            // 复制操作放后台线程，避免界面卡住。
            var report = await Task.Run(() =>
                LegacyLayoutMigrator.Migrate(AppPaths.InstallRoot, AppPaths.DataRoot, Core.App.CurrentLogger));

            // 归档旧目录是可选项，放在迁移成功之后。
            if (ArchiveCheck.IsChecked == true && !report.HasFailures)
            {
                await Task.Run(() => LegacyLayoutMigrator.ArchiveLegacyDirectories(AppPaths.InstallRoot));
            }

            ShowReport(report);
            MarkPrompted();
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Error($"数据迁移失败：{ex.Message}");
            ResultSummary.Text = $"迁移过程中出错：{ex.Message}";
            ResultPanel.IsVisible = true;
        }
        finally
        {
            BusyRing.IsVisible = false;
            MigrateButton.IsEnabled = false;
        }
    }

    private void ShowReport(MigrationReport report)
    {
        ResultSummary.Text = report.AnythingMoved
            ? $"迁移完成。{report.Summary}"
            : $"没有需要迁移的内容。{report.Summary}";

        ResultList.ItemsSource = report.Results.Select(result => new MigrationResultRow(
            result.Outcome switch
            {
                MigrationOutcome.Migrated => "✅",
                MigrationOutcome.Skipped => "⏭",
                _ => "⚠"
            },
            result.Label,
            result.Outcome switch
            {
                MigrationOutcome.Migrated => $"已复制 {result.FilesMoved} 个文件到 {Shorten(result.Target)}",
                MigrationOutcome.Skipped => "目标已存在同名文件，按「不覆盖」原则跳过",
                _ => $"失败：{result.Error}"
            })).ToList();

        ResultPanel.IsVisible = true;

        // 迁移后配置已改变，必须重启才能让新路径生效。
        RestartButton.IsVisible = report.AnythingMoved;
    }

    private void Skip_Click(object? sender, RoutedEventArgs e)
    {
        // 跳过不等于「以后别问」：标记已询问，避免反复打扰，
        // 但用户随时可以从工具箱重新打开。
        MarkPrompted();
        Dismiss();
    }

    private void Restart_Click(object? sender, RoutedEventArgs e)
    {
        Core.AppPaths.RestartViaLauncher();
        Environment.Exit(0);
    }

    private void Dismiss()
    {
        // 向导始终以 FAContentDialog 的内容呈现，关闭交给对话框本身；
        // 这里保留独立窗口承载的兼容分支（VisualRoot 在 Avalonia 12 中取代了 GetVisualRoot()）。
        if (VisualRoot is Window window)
        {
            window.Close();
        }
    }

    // ---------- 辅助 ----------

    private static string Shorten(string path)
    {
        try
        {
            var relative = Path.GetRelativePath(AppPaths.InstallRoot, path);
            return relative == "."
                ? AppPaths.DataFolderName
                : string.Join(Path.DirectorySeparatorChar.ToString(), relative.Split(Path.DirectorySeparatorChar));
        }
        catch
        {
            return path;
        }
    }

    private static string FormatSize(string directory)
    {
        try
        {
            return ToolboxService.FormatFileSize(ToolboxService.GetDirectorySize(directory));
        }
        catch
        {
            return "--";
        }
    }
}

/// <summary>
///     迁移向导「将要迁移的内容」列表行。
///     <para>
///         必须是顶层公开类型：本项目启用了
///         <c>AvaloniaUseCompiledBindingsByDefault</c>，DataTemplate 的 <c>x:DataType</c>
///         需要能在 XAML 编译期解析到该类型，嵌套私有类型无法引用。
///         属性用 <c>get; set;</c> 而非 record 位置参数，以兼容编译期绑定。
///     </para>
/// </summary>
public sealed class MigrationPlanRow
{
    public MigrationPlanRow(string icon, string label, string path, string size)
    {
        Icon = icon;
        Label = label;
        Path = path;
        Size = size;
    }

    public string Icon { get; }

    public string Label { get; }

    public string Path { get; }

    public string Size { get; }
}

/// <summary>迁移向导「迁移结果」列表行。同样必须是顶层公开类型。</summary>
public sealed class MigrationResultRow
{
    public MigrationResultRow(string icon, string label, string detail)
    {
        Icon = icon;
        Label = label;
        Detail = detail;
    }

    public string Icon { get; }

    public string Label { get; }

    public string Detail { get; }
}
