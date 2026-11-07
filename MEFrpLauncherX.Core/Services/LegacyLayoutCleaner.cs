namespace MEFrpLauncherX.Core.Services;

/// <summary>
///     安装根「旧版残留」收拢（26.5.1）。
///     <para>
///         <b>问题：</b>≤26.4 是单层扁平布局，程序文件全部平铺在安装根（<c>Resources\</c>、
///         <c>runtimes\</c>、<c>Tools\</c>、<c>bin\</c>、<c>Config\</c> …）。26.5 起代码移进
///         <c>vXXX\</c>、数据移进 <c>data\</c>，但安装器只会「新增 / 覆盖」，不会删除旧文件，
///         于是升级后安装根会同时存在新旧两套结构；数据迁移向导归档出的
///         <c>*.old-yyyyMMdd_HHmmss\</c> 也会堆在根目录。
///     </para>
///     <para>
///         <b>做法：</b>把安装根下<b>不属于当前布局</b>的文件与目录一律搬进 <c>legacy\</c>，
///         让安装根只剩「启动器 + <c>launcher.json</c> + <c>data\</c> + <c>vXXX\</c>」。
///         只搬移、不删除 —— 删除必须由用户在提示框里确认（见 <see cref="InspectLegacy" />）。
///     </para>
///     <para>
///         <b>安全约束（顺序即优先级）：</b>
///         <list type="number">
///             <item>只在版本化布局下工作：开发态 / 扁平布局的安装根本身就是代码目录，绝不动；</item>
///             <item>白名单优先：启动器、<c>launcher.json</c>、<c>data\</c>、<c>legacy\</c>、
///                   所有 <c>vXXX\</c>、卸载器与系统隐藏文件永不搬移；</item>
///             <item>数据迁移尚未完成的旧目录（<c>Config\</c> / <c>bin\</c> 等）保护起来，
///                   否则用户稍后从「工具箱 → 数据迁移向导」就找不到源数据了；</item>
///             <item>搬移失败（文件被占用 / 权限不足）只记日志，不影响启动。</item>
///         </list>
///     </para>
/// </summary>
public static class LegacyLayoutCleaner
{
    /// <summary>允许留在安装根的文件名（不区分大小写）。</summary>
    private static readonly string[] KeepFileNames =
    [
        // 版本槽位与启动器日志
        "launcher.json",
        "launcher.json.tmp",
        "launcher.log",
        // Windows / 资源管理器的系统隐藏文件：搬走会破坏文件夹视图
        "desktop.ini",
        "thumbs.db",
        ".ds_store"
    ];

    /// <summary>
    ///     数据迁移向导的源目录名：当 <see cref="LegacyLayoutMigrator.BuildPlan" /> 仍把它们列为
    ///     待迁移项时，说明数据还没搬进 <c>data\</c>，搬进 <c>legacy\</c> 会让向导失效。
    /// </summary>
    private static readonly string[] MigrationSourceNames = ["Config", "Cache", "Logs", "bin", "Plugins"];

    /// <summary>安装根下的一项残留。</summary>
    /// <param name="Name">条目名（相对安装根）</param>
    /// <param name="FullPath">完整路径</param>
    /// <param name="IsDirectory">是否为目录</param>
    /// <param name="SizeBytes">体积（目录递归统计；文件取长度）</param>
    public sealed record Leftover(string Name, string FullPath, bool IsDirectory, long SizeBytes);

    /// <summary>一次收拢的结果。</summary>
    public sealed record SweepResult
    {
        /// <summary>已搬进 <c>legacy\</c> 的条目。</summary>
        public IReadOnlyList<Leftover> Moved { get; init; } = [];

        /// <summary>搬移失败的条目（被占用 / 权限不足）。</summary>
        public IReadOnlyList<Leftover> Failed { get; init; } = [];

        /// <summary>已搬移条目的总体积（判断「值不值得提示」用）。</summary>
        public long MovedBytes { get; init; }

        /// <summary>本次是否真的搬了什么。</summary>
        public bool AnythingMoved => Moved.Count > 0;
    }

    /// <summary>
    ///     扫描安装根，列出<b>不属于当前布局</b>的条目（不执行任何搬移）。
    /// </summary>
    public static IReadOnlyList<Leftover> Inspect()
    {
        var result = new List<Leftover>();

        if (!AppPaths.IsVersionedLayout)
        {
            // 开发态 / ≤26.4 扁平布局：安装根就是代码目录，这里没有「残留」的概念
            return result;
        }

        var installRoot = AppPaths.InstallRoot;
        if (!Directory.Exists(installRoot))
        {
            return result;
        }

        var protectedNames = GetMigrationProtectedNames(installRoot);

        foreach (var path in SafeEnumerateEntries(installRoot))
        {
            var name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(name) || IsCurrentLayoutEntry(name) ||
                protectedNames.Contains(name))
            {
                continue;
            }

            // 正在运行的可执行文件（异常启动方式下可能就在安装根）绝不能搬
            if (IsRunningExecutable(path))
            {
                continue;
            }

            var isDirectory = Directory.Exists(path);
            result.Add(new Leftover(name, path, isDirectory, GetSize(path, isDirectory)));
        }

        return result;
    }

    /// <summary>
    ///     把安装根下所有非当前布局的条目搬进 <c>legacy\</c>（幂等；无可搬移项时为空操作）。
    /// </summary>
    /// <param name="logger">日志器（可为 null）</param>
    public static SweepResult Sweep(LogUtil? logger = null)
    {
        if (!AppPaths.IsVersionedLayout)
        {
            return new SweepResult();
        }

        var leftovers = Inspect();
        if (leftovers.Count == 0)
        {
            return new SweepResult();
        }

        var legacyRoot = AppPaths.LegacyDirectory;
        var moved = new List<Leftover>();
        var failed = new List<Leftover>();
        long movedBytes = 0;

        try
        {
            Directory.CreateDirectory(legacyRoot);
        }
        catch (Exception ex)
        {
            logger?.Warning($"创建 legacy 目录失败：{ex.Message}", module: EnumLogModule.Update);
            return new SweepResult { Failed = leftovers };
        }

        foreach (var item in leftovers)
        {
            var target = Path.Combine(legacyRoot, item.Name);

            // 同名冲突（上一次收拢后用户又从别处拷回来）：加时间戳区分，绝不覆盖已有内容
            if (File.Exists(target) || Directory.Exists(target))
            {
                target = Path.Combine(legacyRoot, $"{item.Name}.{DateTime.Now:yyyyMMdd_HHmmss}");
            }

            try
            {
                if (item.IsDirectory)
                {
                    Directory.Move(item.FullPath, target);
                }
                else
                {
                    File.Move(item.FullPath, target);
                }

                moved.Add(item);
                movedBytes += item.SizeBytes;
                logger?.Log($"已把旧版残留移入 {AppPaths.LegacyFolderName}\\：{item.Name}",
                    module: EnumLogModule.Update);
            }
            catch (Exception ex)
            {
                failed.Add(item);
                logger?.Warning($"旧版残留搬移失败（可能被占用）：{item.Name}（{ex.Message}）",
                    module: EnumLogModule.Update);
            }
        }

        logger?.Log(
            $"安装根旧版残留收拢完成：搬移 {moved.Count} 项（{ToolboxService.FormatFileSize(movedBytes)}），" +
            $"失败 {failed.Count} 项",
            module: EnumLogModule.Update);

        return new SweepResult { Moved = moved, Failed = failed, MovedBytes = movedBytes };
    }

    /// <summary>
    ///     列出 <c>legacy\</c> 中现有的条目（供提示框展示体积与数量）。
    ///     目录不存在时返回空列表。
    /// </summary>
    public static IReadOnlyList<Leftover> InspectLegacy()
    {
        var result = new List<Leftover>();
        var legacyRoot = AppPaths.LegacyDirectory;

        if (!Directory.Exists(legacyRoot))
        {
            return result;
        }

        foreach (var path in SafeEnumerateEntries(legacyRoot))
        {
            var name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var isDirectory = Directory.Exists(path);
            result.Add(new Leftover(name, path, isDirectory, GetSize(path, isDirectory)));
        }

        return result;
    }

    /// <summary>
    ///     删除整个 <c>legacy\</c> 目录（用户确认后的动作）。
    /// </summary>
    /// <returns>(删除失败的条目数, 回收字节数)</returns>
    public static (int Failed, long FreedBytes) DeleteLegacy(LogUtil? logger = null)
    {
        var legacyRoot = AppPaths.LegacyDirectory;
        if (!Directory.Exists(legacyRoot))
        {
            return (0, 0);
        }

        long freed = 0;
        var failed = 0;

        // 逐项删除：某一项被占用时其余项仍能清掉，不会因为一次异常整批放弃
        foreach (var item in InspectLegacy())
        {
            try
            {
                if (item.IsDirectory)
                {
                    Directory.Delete(item.FullPath, true);
                }
                else
                {
                    File.Delete(item.FullPath);
                }

                freed += item.SizeBytes;
            }
            catch (Exception ex)
            {
                failed++;
                logger?.Warning($"删除旧版残留失败（可能被占用）：{item.Name}（{ex.Message}）",
                    module: EnumLogModule.Update);
            }
        }

        // 全部删干净后连同 legacy\ 目录一起移除，避免残留一个空文件夹
        if (failed == 0)
        {
            try
            {
                if (Directory.Exists(legacyRoot) && !SafeEnumerateEntries(legacyRoot).Any())
                {
                    Directory.Delete(legacyRoot, false);
                }
            }
            catch
            {
                // 空目录删不掉无伤大雅
            }
        }

        logger?.Log($"已删除 legacy 中的旧版残留：回收 {ToolboxService.FormatFileSize(freed)}，失败 {failed} 项",
            module: EnumLogModule.Update);

        return (failed, freed);
    }

    /// <summary>
    ///     该名字是否属于当前布局（应留在安装根）。
    /// </summary>
    public static bool IsCurrentLayoutEntry(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return true;
        }

        if (string.Equals(name, AppPaths.DataFolderName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, AppPaths.LegacyFolderName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 版本目录（v + 数字）
        if (AppPaths.IsVersionFolderName(name))
        {
            return true;
        }

        // 启动器（文件名含空格，Windows 带 .exe）
        if (string.Equals(name, "PML 2", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "PML 2.exe", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (KeepFileNames.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        // Inno Setup 卸载器（unins000.exe / .dat / .msg）：卸载流程依赖它们，不能搬
        return IsInnoUninstaller(name);
    }

    /// <summary>Inno Setup 生成的卸载器文件（<c>unins000.exe</c> / <c>unins000.dat</c> / <c>unins000.msg</c>）。</summary>
    private static bool IsInnoUninstaller(string name)
    {
        if (!name.StartsWith("unins", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var extension = Path.GetExtension(name);
        return extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".dat", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".msg", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     数据迁移向导尚未消费的旧目录名集合（这些目录搬走会导致用户数据无法迁移）。
    ///     <para>
    ///         判定方式：向导仍把它列为待迁移项，且对应的 <c>data\</c> 目标目录里<b>一个文件都没
    ///         有</b> —— 说明数据还没搬过去。反之（data 目标里已有内容）说明迁移已完成，
    ///         旧目录已是纯残留，可以收拢（即便归档步骤当时失败了）。
    ///     </para>
    ///     <para>
    ///         即便判定失误也只是“把文件搬进 legacy\”而非删除，用户可在弹窗中打开目录取回。
    ///     </para>
    /// </summary>
    private static HashSet<string> GetMigrationProtectedNames(string installRoot)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var item in LegacyLayoutMigrator.BuildPlan(installRoot, AppPaths.DataRoot))
            {
                var name = Path.GetFileName(item.Source.TrimEnd(Path.DirectorySeparatorChar));
                if (string.IsNullOrEmpty(name) ||
                    !MigrationSourceNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!HasAnyFile(item.Target))
                {
                    names.Add(name);
                }
            }
        }
        catch
        {
            // 规划失败时保守处理：不保护任何目录（BuildPlan 本身只做路径拼接，几乎不会抛）
        }

        return names;
    }

    /// <summary>目标目录里是否至少有一个文件（用于判断迁移是否已完成）。</summary>
    private static bool HasAnyFile(string directory)
    {
        try
        {
            return Directory.Exists(directory) &&
                   Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Any();
        }
        catch
        {
            // 读不到就当“还没迁移”，宁可不搬也不能弄丢数据
            return false;
        }
    }

    /// <summary>该路径是否就是当前进程的可执行文件。</summary>
    private static bool IsRunningExecutable(string path)
    {
        try
        {
            var self = Environment.ProcessPath;
            if (string.IsNullOrEmpty(self))
            {
                return false;
            }

            var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return string.Equals(Path.GetFullPath(path), Path.GetFullPath(self), comparison);
        }
        catch
        {
            return false;
        }
    }

    private static long GetSize(string path, bool isDirectory) =>
        isDirectory
            ? ToolboxService.GetDirectorySize(path)
            : SafeFileSize(path);

    private static long SafeFileSize(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch
        {
            return 0;
        }
    }

    private static IEnumerable<string> SafeEnumerateEntries(string directory)
    {
        try
        {
            return Directory.EnumerateFileSystemEntries(directory);
        }
        catch
        {
            return [];
        }
    }
}
