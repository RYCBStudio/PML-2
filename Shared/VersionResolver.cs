// 本文件同时编译进 MEFrpLauncherX.Core（namespace 为 Core）与 PML2.Launcher（namespace 为 Launcher）。
// 因此：
//   1) 不使用任何 using，全部显式写全限定名；
//   2) 不使用 nullable 引用注解（两个项目的 <Nullable> 设置不同）；
//   3) 不使用 record / 源生成序列化，避免两个项目间的元数据差异。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PML2.Shared;

/// <summary>版本槽位文件的 schema 版本。</summary>
internal static class LauncherSchema
{
    internal const int Current = 1;

    /// <summary>版本目录前缀，形如 v26.5.0。</summary>
    internal const string VersionPrefix = "v";

    /// <summary>数据目录名。</summary>
    internal const string DataFolderName = "data";

    /// <summary>槽位文件名（位于安装根）。</summary>
    internal const string ManifestFileName = "launcher.json";

    /// <summary>主程序可执行文件名。</summary>
    internal const string AppExecutableName = "MEFrpLauncherX.exe";

    /// <summary>主程序可执行文件名（Unix，无扩展名）。</summary>
    internal const string AppExecutableNameUnix = "MEFrpLauncherX";

    /// <summary>判断目录名是否为版本目录（v + 数字）。</summary>
    internal static bool IsVersionFolder(string name)
    {
        return !string.IsNullOrEmpty(name) && name.Length > 1 &&
               (name[0] == 'v' || name[0] == 'V') && char.IsDigit(name[1]);
    }

    /// <summary>本机主程序文件名。</summary>
    internal static string AppExecutable
    {
        get { return OperatingSystem.IsWindows() ? AppExecutableName : AppExecutableNameUnix; }
    }
}

/// <summary>安装根下的 launcher.json：记录当前启用的版本槽位与回滚信息。</summary>
internal sealed class LauncherManifest
{
    internal int Schema { get; set; } = LauncherSchema.Current;

    /// <summary>当前启用的版本目录名，形如 v26.5.0。</summary>
    internal string Current { get; set; } = "";

    /// <summary>上一个版本目录名，供回滚；无则为空。</summary>
    internal string Previous { get; set; } = "";

    /// <summary>该版本是否为 AOT 产物（更新时据此选择安装包）。</summary>
    internal bool Aot { get; set; } = true;

    /// <summary>写入时间（UTC），仅供诊断。</summary>
    internal DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>一次「挑选要启动的版本」的解析结果。</summary>
internal sealed class LaunchResolution
{
    /// <summary>主程序完整路径；解析失败时为空。</summary>
    internal string ExecutablePath = "";

    /// <summary>数据根目录。</summary>
    internal string DataRoot = "";

    /// <summary>安装根目录。</summary>
    internal string InstallRoot = "";

    /// <summary>实际选中的版本目录名（可能来自 current，也可能来自扫描兜底）。</summary>
    internal string VersionFolder = "";

    /// <summary>解析过程说明，用于写入启动器日志。</summary>
    internal string Detail = "";

    /// <summary>解析是否成功。</summary>
    internal bool Success
    {
        get { return !string.IsNullOrEmpty(ExecutablePath); }
    }
}

/// <summary>
///     版本槽位解析器：决定启动器该拉起哪一个 vXXX 目录中的主程序。
///     <para>
///         优先级（自高而低）：
///         <list type="number">
///             <item><c>--version</c> 命令行参数显式指定。</item>
///             <item><c>launcher.json</c> 的 <c>current</c> 字段。</item>
///             <item>扫描安装根，按目录名版本号取最大者（manifest 缺失/损坏时的兜底）。</item>
///         </list>
///     </para>
/// </summary>
internal static class VersionResolver
{
    /// <summary>解析要启动的版本。</summary>
    internal static LaunchResolution Resolve(string installRoot, string dataRootOverride, string explicitVersion)
    {
        var result = new LaunchResolution
        {
            InstallRoot = installRoot,
            DataRoot = string.IsNullOrWhiteSpace(dataRootOverride)
                ? Path.Combine(installRoot, LauncherSchema.DataFolderName)
                : Path.GetFullPath(dataRootOverride)
        };

        // 1) 显式指定
        if (!string.IsNullOrWhiteSpace(explicitVersion))
        {
            var folder = NormalizeFolderName(explicitVersion);
            var candidate = Path.Combine(installRoot, folder);
            if (File.Exists(Path.Combine(candidate, LauncherSchema.AppExecutable)))
            {
                result.VersionFolder = folder;
                result.ExecutablePath = Path.Combine(candidate, LauncherSchema.AppExecutable);
                result.Detail = $"使用命令行指定的版本 {folder}";
                return result;
            }

            result.Detail = $"命令行指定的版本 {folder} 不存在，回退到自动选择";
        }

        // 2) launcher.json
        var manifest = TryLoadManifest(installRoot);
        if (manifest != null && !string.IsNullOrWhiteSpace(manifest.Current))
        {
            var folder = NormalizeFolderName(manifest.Current);
            var candidate = Path.Combine(installRoot, folder);
            if (File.Exists(Path.Combine(candidate, LauncherSchema.AppExecutable)))
            {
                result.VersionFolder = folder;
                result.ExecutablePath = Path.Combine(candidate, LauncherSchema.AppExecutable);
                result.Detail = $"使用 launcher.json 中的 current = {folder}";
                return result;
            }

            result.Detail = $"launcher.json 指向的 {folder} 缺失，回退到扫描安装根";
        }
        else
        {
            result.Detail = "未找到可用的 launcher.json，扫描安装根";
        }

        // 3) 扫描兜底：取版本号最大的目录
        var newest = FindNewestVersionFolder(installRoot);
        if (newest != null)
        {
            result.VersionFolder = newest;
            result.ExecutablePath = Path.Combine(installRoot, newest, LauncherSchema.AppExecutable);
            result.Detail = $"扫描安装根，选中最新版本 {newest}";
            return result;
        }

        // 4) 开发态回退：主程序就在安装根下（如 dotnet run / bin\Debug）
        var flat = Path.Combine(installRoot, LauncherSchema.AppExecutable);
        if (File.Exists(flat))
        {
            result.VersionFolder = "";
            result.ExecutablePath = flat;
            result.Detail = "开发态布局：主程序位于安装根";
            return result;
        }

        result.Detail = $"未在 {installRoot} 下找到任何可用的 {LauncherSchema.AppExecutable}";
        return result;
    }

    /// <summary>把 "26.5.0" / "v26.5.0" 统一为 "v26.5.0"。</summary>
    internal static string NormalizeFolderName(string version)
    {
        version = version.Trim();
        return LauncherSchema.IsVersionFolder(version) ? version : LauncherSchema.VersionPrefix + version;
    }

    /// <summary>读取 launcher.json；缺失或损坏返回 null。</summary>
    /// <remarks>
    ///     刻意用 <see cref="JsonDocument" /> 而非 <c>JsonSerializer.Deserialize&lt;T&gt;</c>：
    ///     <list type="number">
    ///         <item>序列化器只认 <c>public</c> 成员，而本类型成员是 <c>internal</c>，
    ///               反射方案会静默返回全空对象（于是 launcher.json 形同虚设）；</item>
    ///         <item>启动器以 PublishTrimmed、主程序以 NativeAOT 发布，
    ///               反射式 JSON 在这两种发布下都不可靠。</item>
    ///     </list>
    ///     JsonDocument 是纯前瞻式 API，两种发布下行为一致。
    /// </remarks>
    internal static LauncherManifest? TryLoadManifest(string installRoot)
    {
        try
        {
            var path = Path.Combine(installRoot, LauncherSchema.ManifestFileName);
            if (!File.Exists(path))
            {
                return null;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var schema = root.TryGetProperty("schema", out var schemaElement) &&
                         schemaElement.ValueKind == JsonValueKind.Number &&
                         schemaElement.TryGetInt32(out var schemaValue)
                ? schemaValue
                : LauncherSchema.Current;

            if (schema > LauncherSchema.Current)
            {
                return null;
            }

            var manifest = new LauncherManifest { Schema = schema };

            if (root.TryGetProperty("current", out var current) && current.ValueKind == JsonValueKind.String)
            {
                manifest.Current = current.GetString() ?? "";
            }

            if (root.TryGetProperty("previous", out var previous) && previous.ValueKind == JsonValueKind.String)
            {
                manifest.Previous = previous.GetString() ?? "";
            }

            if (root.TryGetProperty("aot", out var aot) &&
                aot.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                manifest.Aot = aot.GetBoolean();
            }

            if (root.TryGetProperty("updatedAt", out var updatedAt) &&
                updatedAt.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(updatedAt.GetString(), out var parsed))
            {
                manifest.UpdatedAt = parsed;
            }

            return manifest;
        }
        catch
        {
            // manifest 损坏不应阻止启动：扫描兜底会接管。
            return null;
        }
    }

    /// <summary>写入 launcher.json（原子替换，避免半写状态）。</summary>
    /// <remarks>与读取同理，用 <see cref="Utf8JsonWriter" /> 手写结构，不依赖反射。</remarks>
    internal static bool TrySaveManifest(string installRoot, LauncherManifest manifest)
    {
        try
        {
            manifest.UpdatedAt = DateTimeOffset.UtcNow;
            var path = Path.Combine(installRoot, LauncherSchema.ManifestFileName);
            var temp = path + ".tmp";

            byte[] payload;
            using (var buffer = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("schema", manifest.Schema);
                    writer.WriteString("current", manifest.Current ?? "");
                    writer.WriteString("previous", manifest.Previous ?? "");
                    writer.WriteBoolean("aot", manifest.Aot);
                    writer.WriteString("updatedAt",
                        (manifest.UpdatedAt ?? DateTimeOffset.UtcNow).ToString("yyyy-MM-ddTHH:mm:ssZ"));
                    writer.WriteEndObject();
                }

                payload = buffer.ToArray();
            }

            File.WriteAllBytes(temp, payload);

            // File.Move 的 overwrite 参数在部分平台上会先删除目标，
            // 这里显式删除再移动，行为跨平台一致。
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(temp, path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>扫描安装根，返回版本号最大的目录名；没有则返回 null。</summary>
    internal static string? FindNewestVersionFolder(string installRoot)
    {
        try
        {
            if (!Directory.Exists(installRoot))
            {
                return null;
            }

            string? best = null;
            var bestVersion = new Version(0, 0);

            foreach (var dir in Directory.EnumerateDirectories(installRoot))
            {
                var name = Path.GetFileName(dir);
                if (!LauncherSchema.IsVersionFolder(name) ||
                    !File.Exists(Path.Combine(dir, LauncherSchema.AppExecutable)))
                {
                    continue;
                }

                if (!TryParseVersion(name, out var version))
                {
                    continue;
                }

                if (version > bestVersion)
                {
                    bestVersion = version;
                    best = name;
                }
            }

            return best;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>把 "v26.5.0-beta.1" 解析为可比较的 Version（非数字后缀被忽略）。</summary>
    private static bool TryParseVersion(string folderName, out Version version)
    {
        version = new Version(0, 0);

        var trimmed = folderName.Substring(1);

        // 去掉预发布/构建元数据后缀（-beta.1 / +abc）。
        var cut = trimmed.IndexOfAny(new[] { '-', '+' });
        if (cut >= 0)
        {
            trimmed = trimmed.Substring(0, cut);
        }

        return Version.TryParse(trimmed, out version!);
    }

    /// <summary>列出安装根下所有版本目录，供工具箱展示与清理。</summary>
    internal static List<string> ListVersionFolders(string installRoot)
    {
        try
        {
            return Directory.Exists(installRoot)
                ? Directory.EnumerateDirectories(installRoot)
                    .Select(Path.GetFileName)
                    .Where(name => name != null && LauncherSchema.IsVersionFolder(name))
                    .Select(name => name!)
                    .ToList()
                : new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}
