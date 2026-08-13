namespace MEFrpLauncherX.Core.Services;

/// <summary>
///     工具箱服务：提供目录大小统计、旧文件清理、日志导出等通用工具能力。
/// </summary>
public static class ToolboxService
{
    /// <summary>
    ///     递归计算指定目录的总大小（字节）。目录不存在时返回 0。
    /// </summary>
    public static long GetDirectorySize(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            return 0;
        }

        try
        {
            DirectoryInfo di = new DirectoryInfo(path);
            return di.EnumerateFiles("*.*", SearchOption.AllDirectories).Sum(fi => fi.Length);
        }
        catch
        {
            // 目录遍历失败（权限等）时忽略
        }

        return 0;
    }

    /// <summary>
    ///     删除目录中最后写入时间早于 <paramref name="olderThan" /> 的文件（含子目录）。
    ///     正在被占用/无法删除的文件会被跳过，不影响其余文件的清理。
    /// </summary>
    /// <param name="directory">目标目录，不存在时直接返回空结果。</param>
    /// <param name="olderThan">保留时长；传入 <see cref="TimeSpan.MaxValue" /> 表示清理全部文件。</param>
    /// <returns>被删除的文件数与释放的字节数。</returns>
    public static (int DeletedCount, long FreedBytes) CleanOldFiles(string directory, TimeSpan olderThan)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return (0, 0);
        }

        var deadline = olderThan >= TimeSpan.MaxValue
            ? DateTime.MaxValue
            : DateTime.Now - olderThan;

        int deletedCount = 0;
        long freedBytes = 0;

        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            try
            {
                var info = new FileInfo(file);
                if (info.LastWriteTime < deadline)
                {
                    freedBytes += info.Length;
                    File.Delete(file);
                    deletedCount++;
                }
            }
            catch
            {
                // 文件正在使用（如当前日志、正在下载的更新包）或权限不足，跳过
            }
        }

        // 清理后移除已空置的目录
        foreach (var subDir in Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories)
                     .OrderByDescending(d => d.Length))
        {
            try
            {
                if (!Directory.EnumerateFileSystemEntries(subDir).Any())
                {
                    Directory.Delete(subDir, false);
                }
            }
            catch
            {
                // 目录非空或无法删除时忽略
            }
        }

        return (deletedCount, freedBytes);
    }

    /// <summary>
    ///     将日志目录中的日志文件导出到目标目录下的时间戳子目录。
    /// </summary>
    /// <param name="targetDirectory">用户选择的目标目录。</param>
    /// <param name="logDirectory">日志源目录，默认使用运行目录下的 Logs。</param>
    /// <returns>导出后的完整目录路径；没有可导出的日志时返回 null。</returns>
    public static string? ExportLogs(string targetDirectory, string? logDirectory = null)
    {
        logDirectory ??= Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        if (!Directory.Exists(logDirectory))
        {
            return null;
        }

        var logFiles = Directory.GetFiles(logDirectory, "*.log");
        if (logFiles.Length == 0)
        {
            return null;
        }

        var exportDir = Path.Combine(targetDirectory, $"PML2_Logs_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(exportDir);

        foreach (var logFile in logFiles)
        {
            try
            {
                File.Copy(logFile, Path.Combine(exportDir, Path.GetFileName(logFile)), true);
            }
            catch
            {
                // 单个日志复制失败（被占用等）时跳过
            }
        }

        return exportDir;
    }

    /// <summary>
    ///     将字节数格式化为易读的大小文本（B/KB/MB/GB/TB）。
    /// </summary>
    public static string FormatFileSize(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }
}
