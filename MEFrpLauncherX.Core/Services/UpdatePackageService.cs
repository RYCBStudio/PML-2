using System.IO.Compression;

namespace MEFrpLauncherX.Core.Services;

/// <summary>
///     应用内更新包（zip）的落地与版本切换（26.5.1）。
///     <para>
///         <b>包格式语义：</b>Release 上的安装包（exe / dmg / deb / rpm / tar.gz）只面向
///         用户手动下载安装；应用内自动更新**一律**下载 <c>pml2-{version}-{os}-{arch}-{mode}.zip</c>
///         并解压进新的 <c>vXXX\</c> 版本目录，不再调用任何系统安装器。
///     </para>
///     <para>
///         zip 的内容 = 发布工作流中 <c>dist/layout/vXXX/</c> 的<b>内容本身</b>
///         （主程序 + Tools/ + Resources/ + 各 DLL，不含 <c>vXXX</c> 这一层），
///         因此解压目标就是安装根下的新版本目录，与安装器写入的布局逐字节等价。
///     </para>
///     <para>
///         落地流程与 Windows 安装器的 <c>ssPostInstall</c> 保持一致：
///         先解压出新版本目录 → 再写 <c>launcher.json</c>（current 指向新版本、
///         previous 记录回滚点）→ 最后按策略清理历史版本目录。
///         <b>顺序不可颠倒</b>：先把槽位指向一个尚不存在的目录会让应用启动失败。
///     </para>
/// </summary>
public static class UpdatePackageService
{
    /// <summary>更新包扩展名（小写，含点）。</summary>
    public const string PackageExtension = ".zip";

    /// <summary>解压临时目录前缀（位于安装根，成功后重命名为 <c>vXXX</c>）。</summary>
    private const string StagingPrefix = ".update-";

    /// <summary>解压临时目录后缀。</summary>
    private const string StagingSuffix = ".staging";

    /// <summary>一次更新落地的结果。</summary>
    public sealed record ApplyResult
    {
        /// <summary>是否成功完成解压 + 版本切换。</summary>
        public bool Success { get; init; }

        /// <summary>新版本目录名（形如 <c>v26.5.1</c>）；失败时为空串。</summary>
        public string VersionFolder { get; init; } = "";

        /// <summary>本次清理掉的历史版本目录名。</summary>
        public IReadOnlyList<string> RemovedVersions { get; init; } = [];

        /// <summary>失败原因（已写入日志；供上层决定提示文案）。</summary>
        public string? Error { get; init; }
    }

    /// <summary>文件名是否为本模块认识的更新包（zip）。</summary>
    public static bool IsPackageFile(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && fileName.EndsWith(PackageExtension, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     把下载好的 zip 落地为新的版本目录，并把 <c>launcher.json</c> 切换到该版本。
    /// </summary>
    /// <param name="packagePath">已下载的更新包路径</param>
    /// <param name="version">目标版本号（可带前导 v）</param>
    /// <param name="removeOldVersions">
    ///     true 表示连回滚点一起清理（对应设置里的「删除旧版本」）；
    ///     false 表示保留回滚点（对应「保留旧版本」）。
    /// </param>
    /// <param name="logger">日志器（可为 null）</param>
    public static ApplyResult Apply(string packagePath, string version, bool removeOldVersions,
        LogUtil? logger = null)
    {
        var installRoot = AppPaths.InstallRoot;
        var folderName = AppPaths.VersionFolderPrefix + NormalizeVersion(version);

        // ---------- 前置校验 ----------
        if (!AppPaths.IsVersionedLayout)
        {
            // 开发态 / ≤26.4 扁平布局：没有启动器与版本槽位，zip 无法自举
            return Fail(logger, "当前不是版本化安装布局（缺少启动器与 launcher.json），无法通过 zip 更新");
        }

        if (!AppPaths.IsVersionFolderName(folderName))
        {
            return Fail(logger, $"版本号不合法，无法生成版本目录名: {folderName}");
        }

        if (!File.Exists(packagePath))
        {
            return Fail(logger, $"更新包不存在: {packagePath}");
        }

        var target = Path.Combine(installRoot, folderName);

        // 绝不替换正在运行的版本目录：Windows 下其可执行文件被占用，删除必然半途失败
        if (PathEquals(target, AppPaths.VersionDirectory))
        {
            return Fail(logger, $"目标版本 {folderName} 就是当前运行的版本，无法原地替换");
        }

        var staging = Path.Combine(installRoot, StagingPrefix + folderName + StagingSuffix);

        // ---------- 1. 解压到临时目录 ----------
        try
        {
            // 上一次失败（例如解压中断）可能残留临时目录，先清掉
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, true);
            }

            Directory.CreateDirectory(staging);
            logger?.Log($"正在解压更新包 {Path.GetFileName(packagePath)} -> {staging}",
                module: EnumLogModule.Update);

            ExtractSafely(packagePath, staging);
        }
        catch (Exception ex)
        {
            TryDeleteDirectory(staging, logger);
            return Fail(logger, $"解压更新包失败: {ex.Message}", ex);
        }

        // ---------- 2. 校验包内容 ----------
        var executableName = OperatingSystem.IsWindows() ? "MEFrpLauncherX.exe" : "MEFrpLauncherX";
        var executable = Path.Combine(staging, executableName);
        if (!File.Exists(executable))
        {
            TryDeleteDirectory(staging, logger);
            return Fail(logger, $"更新包内缺少 {executableName}，拒绝安装（包可能已损坏或选错了平台）");
        }

        // ---------- 3. 落位为新版本目录 ----------
        try
        {
            if (Directory.Exists(target))
            {
                // 同一版本目录已存在（上次解压中断 / 手动放置过）：整目录替换，
                // 避免新旧文件混在一起产生难以诊断的加载错误。
                logger?.Log($"目标版本目录已存在，先删除重建: {target}", module: EnumLogModule.Update);
                Directory.Delete(target, true);
            }

            Directory.Move(staging, target);
        }
        catch (Exception ex)
        {
            TryDeleteDirectory(staging, logger);
            return Fail(logger, $"写入版本目录 {folderName} 失败: {ex.Message}", ex);
        }

        EnsureUnixExecutableBits(target, logger);

        // ---------- 4. 切换版本槽位 ----------
        // 回滚点默认是「当前正在运行的版本目录」；用户选择「删除旧版本」时不保留回滚点。
        //
        // 注意两个语义必须分开：
        //   runningFolder —— 本进程真实所在的版本目录（Windows 下无法删除，清理时要跳过）；
        //   previous      —— 写进 launcher.json 的回滚点（优先取 runningFolder）。
        // 开发态下 runningFolder 可能是 bin 之类的非版本目录名，此时不能用它做回滚点，
        // 否则写入 launcher.json 会被参数校验拒绝。
        var runningFolder = AppPaths.CurrentVersionFolder;
        if (!AppPaths.IsVersionFolderName(runningFolder))
        {
            runningFolder = "";
        }

        var previous = removeOldVersions
            ? ""
            : ResolveRollbackFolder(runningFolder, folderName);

        if (!AppPaths.TryWriteVersionSlot(folderName, previous, out var slotError))
        {
            // 槽位没写成功时不要清理任何历史目录：旧版本仍是可启动的唯二选择之一
            return Fail(logger, $"写入 launcher.json 失败: {slotError}");
        }

        logger?.Log($"已切换版本槽位: current={folderName} previous={(string.IsNullOrEmpty(previous) ? "(无)" : previous)}",
            module: EnumLogModule.Update);

        // ---------- 5. 清理历史版本目录 ----------
        var removed = PruneOtherVersions(folderName, previous, runningFolder, logger);

        return new ApplyResult
        {
            Success = true,
            VersionFolder = folderName,
            RemovedVersions = removed
        };
    }

    /// <summary>
    ///     计算回滚点：优先用当前运行的版本目录，其次回退到 launcher.json 里记录的 current。
    ///     <para>
    ///         回退分支只用于“启动方式不常规”（例如调试器直接拉起某个版本）的场景，
    ///         它能保证 launcher.json 里仍有可用回滚点，而不是被清空。
    ///     </para>
    /// </summary>
    private static string ResolveRollbackFolder(string runningFolder, string newFolder)
    {
        if (!string.IsNullOrEmpty(runningFolder) &&
            !string.Equals(runningFolder, newFolder, StringComparison.OrdinalIgnoreCase))
        {
            return runningFolder;
        }

        var fromManifest = AppPaths.CurrentVersionFolderFromManifest();
        return AppPaths.IsVersionFolderName(fromManifest) &&
               !string.Equals(fromManifest, newFolder, StringComparison.OrdinalIgnoreCase)
            ? fromManifest
            : "";
    }

    /// <summary>
    ///     删除安装根下除「新版本」「回滚点」「当前运行版本」之外的历史版本目录。
    ///     <para>
    ///         与安装器的 <c>PruneOldVersions</c> 语义一致：只处理 <c>v+数字</c> 形式的目录，
    ///         绝不触碰 <c>data\</c>；删除失败（文件被占用）只记日志，不影响新版本可用性。
    ///     </para>
    /// </summary>
    private static IReadOnlyList<string> PruneOtherVersions(string keepFolder, string keepPrevious,
        string runningFolder, LogUtil? logger)
    {
        var removed = new List<string>();

        foreach (var folder in AppPaths.ListInstalledVersions())
        {
            if (string.Equals(folder, keepFolder, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(folder, keepPrevious, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(folder, runningFolder, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var path = Path.Combine(AppPaths.InstallRoot, folder);
            try
            {
                Directory.Delete(path, true);
                removed.Add(folder);
                logger?.Log($"已清理历史版本目录: {folder}", module: EnumLogModule.Update);
            }
            catch (Exception ex)
            {
                logger?.Warning($"历史版本目录清理失败（可能被占用），保留: {folder}（{ex.Message}）",
                    module: EnumLogModule.Update);
            }
        }

        // 当前正在运行的旧版本在 Windows 下删不掉：留给下次启动的「清理旧版本」入口。
        // 选择「删除旧版本」时回滚点已置空，它会在新版本启动后显示为可清理项。
        if (!string.IsNullOrEmpty(runningFolder) &&
            !string.Equals(runningFolder, keepFolder, StringComparison.OrdinalIgnoreCase))
        {
            logger?.Log($"当前运行的版本目录 {runningFolder} 无法在运行期间删除，可重启后清理",
                module: EnumLogModule.Update);
        }

        return removed;
    }

    /// <summary>
    ///     解压前逐条校验条目路径，再做解压（防御 zip-slip：条目跳到安装根之外）。
    /// </summary>
    private static void ExtractSafely(string packagePath, string destination)
    {
        var root = Path.GetFullPath(destination);
        var prefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        using (var archive = ZipFile.OpenRead(packagePath))
        {
            if (archive.Entries.Count == 0)
            {
                throw new InvalidDataException("更新包为空");
            }

            foreach (var entry in archive.Entries)
            {
                // 目录条目可能以 "./" / "." 形式出现（不同 zip 工具差异），
                // 归一化后等于解压根本身，属正常。
                var target = Path.GetFullPath(Path.Combine(root, entry.FullName));
                if (string.Equals(target, root, PathComparison))
                {
                    continue;
                }

                if (!target.StartsWith(prefix, PathComparison))
                {
                    throw new InvalidDataException($"更新包包含越界路径: {entry.FullName}");
                }
            }
        }

        // 覆盖写入：临时目录是刚创建的，理论上不会有冲突，但绝不因单个文件存在而失败
        ZipFile.ExtractToDirectory(packagePath, destination, true);
    }

    /// <summary>
    ///     Unix 上补回可执行位。
    ///     <para>
    ///         zip 归档里虽然保存了 Unix 模式位，但 .NET 的 <see cref="ZipFile" /> 解压不会还原它们，
    ///         因此解压后的主程序与 <c>Tools/</c> 下的辅助程序会缺少可执行位，
    ///         表现为「启动器报找不到可执行文件」或「splash 无法启动」。
    ///     </para>
    /// </summary>
    private static void EnsureUnixExecutableBits(string versionDirectory, LogUtil? logger)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        const UnixFileMode executableMode = UnixFileMode.UserRead | UnixFileMode.UserWrite |
                                            UnixFileMode.UserExecute | UnixFileMode.GroupRead |
                                            UnixFileMode.GroupExecute | UnixFileMode.OtherRead |
                                            UnixFileMode.OtherExecute;

        var targets = new List<string>
        {
            Path.Combine(versionDirectory, "MEFrpLauncherX")
        };

        var tools = Path.Combine(versionDirectory, "Tools");
        if (Directory.Exists(tools))
        {
            targets.AddRange(Directory.EnumerateFiles(tools, "*", SearchOption.AllDirectories));
        }

        targets.AddRange(Directory.EnumerateFiles(versionDirectory, "*.so", SearchOption.TopDirectoryOnly));
        targets.AddRange(Directory.EnumerateFiles(versionDirectory, "*.dylib", SearchOption.TopDirectoryOnly));

        foreach (var file in targets)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.SetUnixFileMode(file, executableMode);
                }
            }
            catch (Exception ex)
            {
                logger?.Warning($"设置可执行位失败: {file}（{ex.Message}）", module: EnumLogModule.Update);
            }
        }
    }

    /// <summary>把 <c>v26.5.1</c> / <c>26.5.1</c> 统一成不带前缀的版本号。</summary>
    private static string NormalizeVersion(string version)
    {
        var trimmed = (version ?? "").Trim();
        return trimmed.Length > 1 && (trimmed[0] is 'v' or 'V') && char.IsDigit(trimmed[1])
            ? trimmed[1..]
            : trimmed;
    }

    private static bool PathEquals(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            PathComparison);

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static void TryDeleteDirectory(string path, LogUtil? logger)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch (Exception ex)
        {
            logger?.Warning($"清理临时目录失败: {path}（{ex.Message}）", module: EnumLogModule.Update);
        }
    }

    private static ApplyResult Fail(LogUtil? logger, string error, Exception? ex = null)
    {
        if (ex is null)
        {
            logger?.Warning($"应用内更新失败：{error}", module: EnumLogModule.Update);
        }
        else
        {
            logger?.Error(ex, $"应用内更新失败：{error}");
        }

        return new ApplyResult { Success = false, Error = error };
    }
}
