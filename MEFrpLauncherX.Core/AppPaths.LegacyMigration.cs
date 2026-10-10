using System.Text.Json.Serialization;

namespace MEFrpLauncherX.Core;

/// <summary>
///     单个待迁移项（旧位置 → 新位置）。
/// </summary>
/// <param name="Label">面向用户展示的中文名称。</param>
/// <param name="Source">旧位置（可能不存在）。</param>
/// <param name="Target">新位置。</param>
public readonly record struct LegacyMigrationItem(string Label, string Source, string Target);

/// <summary>迁移结果分类。</summary>
public enum MigrationOutcome
{
    /// <summary>已成功迁移。</summary>
    Migrated,

    /// <summary>目标已存在同名文件，按「不覆盖用户数据」原则跳过。</summary>
    Skipped,

    /// <summary>失败，原因见 <see cref="MigrationResult.Error" />。</summary>
    Failed
}

/// <summary>单项迁移结果。</summary>
/// <param name="Item">对应的待迁移项。</param>
/// <param name="Outcome">结果分类。</param>
/// <param name="FilesMoved">实际移动的文件数。</param>
/// <param name="Error">失败原因（仅 <see cref="MigrationOutcome.Failed" /> 时非空）。</param>
public readonly record struct MigrationResult(LegacyMigrationItem Item, MigrationOutcome Outcome, int FilesMoved, string? Error)
{
    public string Label => Item.Label;

    public string Source => Item.Source;

    public string Target => Item.Target;
}

/// <summary>一次迁移的完整报告。</summary>
public sealed class MigrationReport
{
    /// <summary>是否存在任何旧版数据需要迁移。</summary>
    [JsonIgnore] public bool HasWork => Results.Count > 0;

    /// <summary>是否已成功迁移过（用于决定是否还需展示向导）。</summary>
    [JsonIgnore] public bool AnythingMoved => Results.Any(r => r.Outcome == MigrationOutcome.Migrated);

    /// <summary>是否存在失败项。</summary>
    [JsonIgnore] public bool HasFailures => Results.Any(r => r.Outcome == MigrationOutcome.Failed);

    /// <summary>逐项结果。</summary>
    public List<MigrationResult> Results { get; init; } = [];

    /// <summary>总量汇总，供 UI 显示。</summary>
    [JsonIgnore]
    public string Summary =>
        $"共 {Results.Count} 项，成功迁移 {Results.Count(r => r.Outcome == MigrationOutcome.Migrated)} 项，" +
        $"跳过 {Results.Count(r => r.Outcome == MigrationOutcome.Skipped)} 项，" +
        $"失败 {Results.Count(r => r.Outcome == MigrationOutcome.Failed)} 项。";
}

/// <summary>
///     26.5.0「代码与数据分离」布局的迁移引擎。
///     <para>
///         旧版（≤26.4）把所有内容平铺在程序根目录：
///         <c>Config\</c> / <c>Cache\</c> / <c>Logs\</c> / <c>bin\</c> / <c>Plugins\</c>。
///         新布局把它们收进 <c>data\</c>，代码移入 <c>vXXX\</c>。
///     </para>
///     <para>
///         <b>设计约束（顺序即优先级）：</b>
///         <list type="number">
///             <item>绝不覆盖已存在的目标文件 —— 用户数据优先于「目录整洁」。</item>
///             <item>
///                 永不删除源文件。旧目录保留在原地，用户可随时手动取回；
///                 这也让迁移可回滚（把 data\ 里的文件移回去即可）。
///             </item>
///             <item>幂等：重复执行结果一致，因此可在每次启动时安全调用。</item>
///             <item>失败不抛出：单个文件被占用不应阻断整个启动流程。</item>
///         </list>
///     </para>
/// </summary>
public static class LegacyLayoutMigrator
{
    /// <summary>迁移完成标记（写在 <c>data\Cache\migration-26.5.json</c>），仅用于诊断与避免重复提示。</summary>
    public const string MarkerFileName = "migration-26.5.json";

    /// <summary>旧目录名 → <c>data\</c> 下目标子目录的映射。</summary>
    private static readonly (string Legacy, string Target)[] Map =
    [
        ("Config", "Config"),
        ("Config/Themes", "Config/Themes"),
        ("Config/Plugins", "Config/Plugins"),
        ("bin", "Run"),
        ("Cache", "Cache"),
        ("Logs", "Logs")
    ];

    /// <summary>构造待迁移项清单（不执行任何 I/O）。</summary>
    public static IReadOnlyList<LegacyMigrationItem> BuildPlan(string installRoot, string dataRoot)
    {
        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Config"] = "配置与隧道定义",
            ["Config/Themes"] = "主题",
            ["Config/Plugins"] = "插件",
            ["bin"] = "运行时下载的程序（mefrpc / lego）",
            ["Cache"] = "缓存",
            ["Logs"] = "日志"
        };

        return Map
            .Select(entry => new LegacyMigrationItem(
                labels[entry.Legacy],
                Path.Combine(installRoot, entry.Legacy.Replace('/', Path.DirectorySeparatorChar)),
                Path.Combine(dataRoot, entry.Target.Replace('/', Path.DirectorySeparatorChar))))
            .Where(item => Directory.Exists(item.Source))
            .ToList();
    }

    /// <summary>预览待迁移项（不执行）。</summary>
    public static MigrationReport Preview()
    {
        var plan = BuildPlan(AppPaths.InstallRoot, AppPaths.DataRoot);
        return new MigrationReport
        {
            Results = plan.Select(item =>
            {
                var count = SafeEnumerateFiles(item.Source);
                return new MigrationResult(item,
                    count > 0 ? MigrationOutcome.Migrated : MigrationOutcome.Skipped, 0, null);
            }).ToList()
        };
    }

    /// <summary>
    ///     执行迁移（幂等）。开发态 / 未处于版本化布局时不做任何事。
    /// </summary>
    public static MigrationReport TryMigrate(LogUtil? logger = null)
    {
        if (!AppPaths.IsVersionedLayout)
        {
            // 开发态（bin\Debug\...）本身就是「单层布局」，data\ 就在其下，无需迁移。
            return new MigrationReport();
        }

        return Migrate(AppPaths.InstallRoot, AppPaths.DataRoot, logger);
    }

    /// <summary>
    ///     执行迁移。<paramref name="dryRun" /> 为 <see langword="true" /> 时只统计不落盘。
    /// </summary>
    public static MigrationReport Migrate(string installRoot, string dataRoot, LogUtil? logger = null,
        bool dryRun = false)
    {
        var report = new MigrationReport();

        foreach (var item in BuildPlan(installRoot, dataRoot))
        {
            report.Results.Add(Transfer(item, dryRun));
        }

        if (!dryRun && report.AnythingMoved)
        {
            WriteMarker(report, logger);
        }

        logger?.Log($"旧版数据迁移：{report.Summary}");
        return report;
    }

    private static MigrationResult Transfer(LegacyMigrationItem item, bool dryRun)
    {
        var moved = 0;

        try
        {
            Directory.CreateDirectory(item.Target);

            foreach (var file in Directory.EnumerateFiles(item.Source, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(item.Source, file);
                var destination = Path.Combine(item.Target, relative);

                if (File.Exists(destination))
                {
                    // 不覆盖：data\ 里的同名文件视为更新，保留之。
                    continue;
                }

                if (dryRun)
                {
                    moved++;
                    continue;
                }

                var parent = Path.GetDirectoryName(destination);
                if (!string.IsNullOrEmpty(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                // 复制而非移动：旧目录保留原地，用户可回滚。
                File.Copy(file, destination, false);
                moved++;
            }

            return new MigrationResult(item, moved > 0 ? MigrationOutcome.Migrated : MigrationOutcome.Skipped, moved,
                null);
        }
        catch (Exception ex)
        {
            return new MigrationResult(item, MigrationOutcome.Failed, moved, ex.Message);
        }
    }

    private static int SafeEnumerateFiles(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Count();
        }
        catch
        {
            return 0;
        }
    }

    private static void WriteMarker(MigrationReport report, LogUtil? logger)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.CacheDirectory);
            var marker = Path.Combine(AppPaths.CacheDirectory, MarkerFileName);

            // 复用主配置序列化上下文会让 Core 依赖模型类型；此处仅记录摘要，手写最小 JSON。
            File.WriteAllText(marker, System.Text.Json.JsonSerializer.Serialize(
                new
                {
                    migratedAt = DateTimeOffset.UtcNow,
                    summary = report.Summary
                }));
        }
        catch (Exception ex)
        {
            logger?.Warning($"写入迁移标记失败：{ex.Message}");
        }
    }

    /// <summary>读取上次迁移时间（UTC）；无记录返回 null。</summary>
    public static DateTimeOffset? GetLastMigrationTime()
    {
        try
        {
            var marker = Path.Combine(AppPaths.CacheDirectory, MarkerFileName);
            if (!File.Exists(marker))
            {
                return null;
            }

            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(marker));
            return doc.RootElement.TryGetProperty("migratedAt", out var value)
                ? value.GetDateTimeOffset()
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>把残留的旧目录改名归档，便于用户确认「已迁移」并自行删除。</summary>
    public static IReadOnlyList<string> ArchiveLegacyDirectories(string installRoot)
    {
        var archived = new List<string>();

        foreach (var name in new[] { "Config", "Cache", "Logs", "bin", "Plugins" })
        {
            var legacy = Path.Combine(installRoot, name);
            if (!Directory.Exists(legacy))
            {
                continue;
            }

            // 时间戳后缀避免与既有归档冲突，也便于用户分辨迁移批次。
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var target = Path.Combine(installRoot, $"{name}.old-{stamp}");

            try
            {
                Directory.Move(legacy, target);
                archived.Add(target);
            }
            catch
            {
                // 归档失败不影响运行，旧目录保持原样即可。
            }
        }

        return archived;
    }
}
