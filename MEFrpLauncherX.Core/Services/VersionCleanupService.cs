namespace MEFrpLauncherX.Core.Services;

/// <summary>
///     版本目录巡检（26.5.0，分离式布局）。
///     <para>
///         升级后安装根会同时留下「当前版本」与「回滚点」两个 <c>vXXX\</c> 目录。
///         这本身是设计如此（回滚需要旧版本仍在磁盘上），但当磁盘吃紧、
///         或用户当初选的是「安装后删除」却又残留了目录时，需要一个统一的巡检入口：
///         列出可清理的版本目录、统计其体积，供 UI 展示与「立即清理」使用。
///     </para>
///     <para>
///         <b>安全约束</b>：永远不返回当前正在运行的版本，也永远不返回
///         <c>launcher.json</c> 里记录的 <c>previous</c>（回滚点）——
///         除非调用方显式要求连回滚点一起删。
///     </para>
/// </summary>
public static class VersionCleanupService
{
    /// <summary>单个版本目录的巡检结果。</summary>
    public sealed record VersionEntry(string FolderName, long SizeBytes, bool IsCurrent, bool IsRollback);

    /// <summary>安装根内版本目录的整体巡检结果。</summary>
    /// <param name="Current">当前正在运行的版本目录名（开发态 / 旧布局时为空）。</param>
    /// <param name="Rollback">launcher.json 记录的 <c>previous</c>，即回滚点。</param>
    /// <param name="Entries">全部版本目录及其体积，按体积从大到小排序。</param>
    /// <param name="Removable">
    ///     可安全删除的目录（不含当前版本，也不含回滚点）。
    /// </param>
    /// <param name="TotalSizeBytes">全部版本目录总体积。</param>
    /// <param name="RemovableSizeBytes"><see cref="Removable" /> 的总体积，即「清理可回收的空间」。</param>
    public sealed record Inspection(
        string Current,
        string Rollback,
        IReadOnlyList<VersionEntry> Entries,
        IReadOnlyList<VersionEntry> Removable,
        long TotalSizeBytes,
        long RemovableSizeBytes);

    /// <summary>
    ///     巡检安装根下的版本目录。
    /// </summary>
    /// <param name="includeRollback">
    ///     true 时把回滚点也算作「可清理」—— 仅在用户明确选择「不留回滚点」时使用。
    /// </param>
    public static Inspection Inspect(bool includeRollback = false)
    {
        var current = AppPaths.CurrentVersionFolder;
        var rollback = AppPaths.PreviousVersionFolder();

        var entries = new List<VersionEntry>();
        long total = 0;

        foreach (var folder in AppPaths.ListInstalledVersions())
        {
            var size = GetFolderSize(folder);
            total += size;

            entries.Add(new VersionEntry(
                folder,
                size,
                string.Equals(folder, current, StringComparison.OrdinalIgnoreCase),
                string.Equals(folder, rollback, StringComparison.OrdinalIgnoreCase)));
        }

        // 体积优先、体积相同则按目录名（版本号升序）—— 让「清理」列表读起来符合直觉
        var ordered = entries
            .OrderByDescending(e => e.SizeBytes)
            .ThenBy(e => e.FolderName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var removable = ordered
            .Where(e => !e.IsCurrent && (includeRollback || !e.IsRollback))
            .ToList();

        return new Inspection(
            current,
            rollback,
            ordered,
            removable,
            total,
            removable.Sum(e => e.SizeBytes));
    }

    /// <summary>
    ///     是否值得提醒用户清理：有可清理目录，且其体积达到阈值（默认 200 MB）。
    ///     <para>
    ///         单纯的「多了一个 vXXX」不算问题（回滚点是刻意保留的），
    ///         只有当可回收空间确实可观时才提示，避免升级几次后反复弹窗。
    ///     </para>
    /// </summary>
    public static bool ShouldNotify(Inspection inspection, long thresholdBytes = 200L * 1024 * 1024)
    {
        // 非版本化布局（开发态 / ≤26.4）没有版本目录概念，不提示
        if (!AppPaths.IsVersionedLayout)
        {
            return false;
        }

        return inspection.Removable.Count > 0 && inspection.RemovableSizeBytes >= thresholdBytes;
    }

    /// <summary>删除指定版本目录，返回是否成功（失败原因记入日志）。</summary>
    public static bool TryRemove(string folderName)
    {
        // 复用 AppPaths 的安全约束：拒绝删除当前版本与回滚点
        if (!AppPaths.TryRemoveVersion(folderName, out var error))
        {
            App.CurrentLogger?.Warning($"删除版本目录失败: {folderName}（{error}）", module: EnumLogModule.Update);
            return false;
        }

        App.CurrentLogger?.Log($"已删除版本目录: {folderName}", module: EnumLogModule.Update);
        return true;
    }

    /// <summary>
    ///     删除全部「可清理」的版本目录（不含当前版本，默认也不含回滚点）。
    /// </summary>
    /// <returns>(成功数, 失败数, 回收字节数)</returns>
    public static (int Removed, int Failed, long FreedBytes) RemoveAllRemovable(bool includeRollback = false)
    {
        var removed = 0;
        var failed = 0;
        long freed = 0;

        foreach (var entry in Inspect(includeRollback).Removable)
        {
            if (TryRemove(entry.FolderName))
            {
                removed++;
                freed += entry.SizeBytes;
            }
            else
            {
                failed++;
            }
        }

        return (removed, failed, freed);
    }

    /// <summary>单个版本目录的体积（字节）；目录不存在或不可读返回 0。</summary>
    private static long GetFolderSize(string folderName)
    {
        try
        {
            var path = Path.Combine(AppPaths.InstallRoot, folderName);
            if (!Directory.Exists(path))
            {
                return 0;
            }

            return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length);
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Warning($"统计版本目录体积失败: {folderName}（{ex.Message}）",
                module: EnumLogModule.Update);
            return 0;
        }
    }
}