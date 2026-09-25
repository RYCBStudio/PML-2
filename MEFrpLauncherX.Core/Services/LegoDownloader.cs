using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;

namespace MEFrpLauncherX.Core.Services;

/// <summary>
///     lego（ACME 客户端）按需下载与校验（26.4）。
///     设计要点：
///     <list type="bullet">
///         <item>主安装包<b>不内置</b> lego，首次使用本机 RID 对应的官方产物下载；</item>
///         <item>校验采用官方 <c>lego_{version}_checksums.txt</c> 中的 SHA-256，避免硬编码摘要过期；</item>
///         <item>解压全部在进程内完成（<see cref="ZipFile" /> / <see cref="TarReader" />），不依赖系统 tar；</item>
///         <item>下载与解压产物位于 <c>Tools/lego/{rid}/lego[.exe]</c>，与主程序隔离。</item>
///     </list>
/// </summary>
public static class LegoDownloader
{
    /// <summary>当前使用的 lego 版本</summary>
    public const string LegoVersion = "5.5.1";

    private static string ToolsRoot
    {
        get;
    } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "lego");

    /// <summary>下载基址（GitHub 官方发布页）</summary>
    private static readonly string[] ReleaseBases =
    [
        $"https://github.com/go-acme/lego/releases/download/v{LegoVersion}/",
        // 备用源：GitHub 直链镜像，主源被墙时回退
        $"https://ghproxy.net/https://github.com/go-acme/lego/releases/download/v{LegoVersion}/"
    ];

    /// <summary>把 .NET RID 映射为 lego 发布物的平台标识。</summary>
    private static string? GetPlatformTag()
    {
        var rid = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
        if (rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase))
        {
            return "windows";
        }

        if (rid.StartsWith("linux-", StringComparison.OrdinalIgnoreCase))
        {
            return "linux";
        }

        if (rid.StartsWith("osx-", StringComparison.OrdinalIgnoreCase))
        {
            return "darwin";
        }

        return null;
    }

    /// <summary>把 .NET RID 映射为 lego 发布物的架构标识（amd64 / arm64）。</summary>
    private static string? GetArchTag()
    {
        var rid = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
        if (rid.EndsWith("-x64", StringComparison.OrdinalIgnoreCase))
        {
            return "amd64";
        }

        if (rid.EndsWith("-arm64", StringComparison.OrdinalIgnoreCase))
        {
            return "arm64";
        }

        return null;
    }

    /// <summary>本机对应的 lego 可执行文件名（Windows 带 .exe）。</summary>
    public static string ExecutableName => OperatingSystem.IsWindows() ? "lego.exe" : "lego";

    /// <summary>本机 lego 可执行文件完整路径（无论是否已下载）。</summary>
    public static string ExecutablePath
    {
        get
        {
            var rid = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
            return Path.Combine(ToolsRoot, rid, ExecutableName);
        }
    }

    /// <summary>是否已下载可用的 lego。</summary>
    public static bool IsInstalled()
    {
        try
        {
            return File.Exists(ExecutablePath);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     下载并解压 lego（已存在则直接返回路径）。
    /// </summary>
    /// <param name="progress">进度回调（0–100，附当前阶段文案）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>可执行文件路径；失败返回 null（原因已记日志）。</returns>
    public static async Task<string?> EnsureAsync(Action<double, string>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            if (IsInstalled())
            {
                return ExecutablePath;
            }

            var platform = GetPlatformTag();
            var arch = GetArchTag();
            if (platform is null || arch is null)
            {
                App.CurrentLogger?.Warning(
                    $"当前平台（{System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier}）没有对应的 lego 发布物");
                return null;
            }

            var isWindows = platform == "windows";
            var assetName = $"lego_v{LegoVersion}_{platform}_{arch}" + (isWindows ? ".zip" : ".tar.gz");
            var checksumsName = $"lego_{LegoVersion}_checksums.txt";

            var rid = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
            var targetDir = Path.Combine(ToolsRoot, rid);
            Directory.CreateDirectory(targetDir);

            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"RYCB-PML2/{App.Version}");

            // 1. 取官方校验表（任一源成功即可）
            progress?.Invoke(5, "正在获取校验信息…"); 
            var checksums = await FetchChecksumsAsync(http, checksumsName, ct);
            if (checksums is null)
            {
                App.CurrentLogger?.Warning("无法获取 lego 校验表，下载中止（避免使用未校验的二进制）");
                return null;
            }

            // 2. 下载压缩包到临时文件
            var tempArchive = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}_{assetName}");
            try
            {
                if (!await DownloadAsync(http, assetName, tempArchive, progress, ct))
                {
                    return null;
                }

                // 3. 校验 SHA-256
                progress?.Invoke(80, "正在校验文件…");
                var actual = await ComputeSha256Async(tempArchive, ct);
                if (!checksums.TryGetValue(assetName, out var expected) ||
                    !string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                {
                    App.CurrentLogger?.Warning($"lego 校验失败：期望 {expected}，实际 {actual}");
                    return null;
                }

                // 4. 进程内解压（zip / tar.gz），仅取出可执行文件
                progress?.Invoke(90, "正在解压…");
                var extracted = isWindows
                    ? ExtractFromZip(tempArchive, ExecutableName, targetDir)
                    : ExtractFromTarGz(tempArchive, ExecutableName, targetDir);
                if (!extracted)
                {
                    return null;
                }

                // 5. Unix 需要可执行权限（对齐 macOS/Linux 的既有教训）
                if (!OperatingSystem.IsWindows())
                {
                    try
                    {
                        File.SetUnixFileMode(ExecutablePath,
                            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                    }
                    catch (Exception ex)
                    {
                        App.CurrentLogger?.Error(ex, "设置 lego 可执行权限失败");
                    }
                }

                progress?.Invoke(100, "完成");
                return ExecutablePath;
            }
            finally
            {
                try
                {
                    if (File.Exists(tempArchive))
                    {
                        File.Delete(tempArchive);
                    }
                }
                catch
                {
                    // 临时文件清理失败不影响主流程
                }
            }
        }
        catch (OperationCanceledException)
        {
            App.CurrentLogger?.Warning("lego 下载已取消");
            return null;
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Error(ex, "下载 lego 失败");
            return null;
        }
    }

    /// <summary>依次尝试主源与备用源下载校验表，返回「文件名 → SHA-256」字典。</summary>
    private static async Task<Dictionary<string, string>?> FetchChecksumsAsync(HttpClient http, string checksumsName,
        CancellationToken ct)
    {
        foreach (var baseUrl in ReleaseBases)
        {
            try
            {
                var text = await http.GetStringAsync(baseUrl + checksumsName, ct);
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    // 格式：<sha256>  <filename>
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        map[parts[1].Trim()] = parts[0].Trim();
                    }
                }

                if (map.Count > 0)
                {
                    return map;
                }
            }
            catch (Exception ex)
            {
                App.CurrentLogger?.Warning($"获取校验表失败（{baseUrl}）：{ex.Message}");
            }
        }

        return null;
    }

    /// <summary>依次尝试主源与备用源下载压缩包。</summary>
    private static async Task<bool> DownloadAsync(HttpClient http, string assetName, string destination,
        Action<double, string>? progress, CancellationToken ct)
    {
        foreach (var baseUrl in ReleaseBases)
        {
            try
            {
                using var res = await http.GetAsync(baseUrl + assetName, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!res.IsSuccessStatusCode)
                {
                    App.CurrentLogger?.Warning($"下载 lego 失败（{baseUrl}）：HTTP {(int)res.StatusCode}");
                    continue;
                }

                var total = res.Content.Headers.ContentLength ?? -1;
                await using var src = await res.Content.ReadAsStreamAsync(ct);
                await using var dst = File.Create(destination);

                var buffer = new byte[81920];
                long read = 0;
                int n;
                while ((n = await src.ReadAsync(buffer, ct)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, n), ct);
                    read += n;
                    if (total > 0)
                    {
                        // 5%–75% 区间用于下载进度
                        progress?.Invoke(5 + 70.0 * read / total, "正在下载 lego…");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                App.CurrentLogger?.Warning($"下载 lego 失败（{baseUrl}）：{ex.Message}");
            }
        }

        return false;
    }

    private static async Task<string> ComputeSha256Async(string file, CancellationToken ct)
    {
        await using var stream = File.OpenRead(file);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>从 zip 中提取指定文件名（忽略目录层级）。</summary>
    private static bool ExtractFromZip(string archivePath, string fileName, string targetDir)
    {
        try
        {
            using var zip = ZipFile.OpenRead(archivePath);
            var entry = zip.Entries.FirstOrDefault(e =>
                string.Equals(Path.GetFileName(e.FullName), fileName, StringComparison.OrdinalIgnoreCase));
            if (entry is null)
            {
                App.CurrentLogger?.Warning($"压缩包中未找到 {fileName}");
                return false;
            }

            var target = Path.Combine(targetDir, fileName);
            entry.ExtractToFile(target, true);
            return true;
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Error(ex, "解压 lego（zip）失败");
            return false;
        }
    }

    /// <summary>从 tar.gz 中提取指定文件名（忽略目录层级）。</summary>
    private static bool ExtractFromTarGz(string archivePath, string fileName, string targetDir)
    {
        try
        {
            using var file = File.OpenRead(archivePath);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            using var reader = new TarReader(gzip);

            while (reader.GetNextEntry() is { } entry)
            {
                if (!string.Equals(Path.GetFileName(entry.Name), fileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var target = Path.Combine(targetDir, fileName);
                entry.ExtractToFile(target, true);
                return true;
            }

            App.CurrentLogger?.Warning($"压缩包中未找到 {fileName}");
            return false;
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Error(ex, "解压 lego（tar.gz）失败");
            return false;
        }
    }
}
