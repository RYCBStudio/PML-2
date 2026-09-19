using System.Collections.Concurrent;

namespace MEFrpLauncherX.Core;

public class AsyncLogWriter : IDisposable
{
    /// <summary>
    ///     内存队列上限：日志风暴（如终端高频输出）下防止无界增长拖垮内存。
    ///     超出后丢弃新日志并计数，队列回落后补写一条汇总。
    /// </summary>
    private const int MaxQueuedEntries = 20000;

    /// <summary>日志文件保留天数，超出后启动时清理。</summary>
    private const int RetentionDays = 14;

    private readonly CancellationTokenSource _cts = new();
    private readonly BlockingCollection<string> _logQueue = new(MaxQueuedEntries);
    private readonly Task? _processingTask;
    private readonly StreamWriter? _writer;
    private int _droppedCount;
    private bool _disposed;

    public AsyncLogWriter(string filePath)
    {
        LogFilePath = filePath;
        try
        {
            // 确保日志目录存在
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            CleanupOldLogs(directory);

            // 追加模式：保留上一次运行的日志，保证崩溃前的日志可追溯。
            // FileShare 允许外部读取/移动；同一会话仅本进程写入。
            _writer = new StreamWriter(new FileStream(filePath, FileMode.Append, FileAccess.Write,
                FileShare.ReadWrite | FileShare.Delete))
            {
                AutoFlush = true
            };
            _writer.WriteLine($"============= 会话开始 {DateTime.Now:yyyy-MM-dd HH:mm:ss} =============");
        }
        catch (Exception ex)
        {
            // 日志文件不可写（权限不足/被占用/磁盘满）不应导致应用无法启动：
            // 回退到系统临时目录重试一次，仍失败则仅保留控制台输出。
            try
            {
                var fallback = Path.Combine(Path.GetTempPath(), "PML2", "Logs",
                    Path.GetFileName(filePath));
                Directory.CreateDirectory(Path.GetDirectoryName(fallback)!);
                _writer = new StreamWriter(new FileStream(fallback, FileMode.Append, FileAccess.Write,
                    FileShare.ReadWrite | FileShare.Delete))
                {
                    AutoFlush = true
                };
                LogFilePath = fallback;
                _writer.WriteLine($"============= 会话开始 {DateTime.Now:yyyy-MM-dd HH:mm:ss} =============");
                _writer.WriteLine($"[警告] 原日志路径不可写，已回退到临时目录: {ex.Message}");
            }
            catch
            {
                _writer = null;
            }
        }

        if (_writer is not null)
        {
            _processingTask = Task.Run(ProcessLogQueue);
        }
    }

    /// <summary>实际写入的日志文件路径（回退后可能与传入路径不同）。</summary>
    public string LogFilePath
    {
        get;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _logQueue.CompleteAdding();
        }
        catch
        {
            // 重复 Dispose 时 CompleteAdding 可能抛异常，忽略
        }

        _cts.Cancel();
        try
        {
            _processingTask?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception)
        {
        }

        _writer?.Dispose();
        _logQueue.Dispose();
        _cts.Dispose();

        GC.SuppressFinalize(this);
    }

    private async Task ProcessLogQueue()
    {
        try
        {
            foreach (var logEntry in _logQueue.GetConsumingEnumerable(_cts.Token))
            {
                try
                {
                    if (_writer is null)
                    {
                        continue;
                    }

                    var dropped = Interlocked.Exchange(ref _droppedCount, 0);
                    if (dropped > 0)
                    {
                        await _writer.WriteLineAsync(
                            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}][警告] 日志队列溢出，已丢弃 {dropped} 条日志。");
                    }

                    await _writer.WriteLineAsync(logEntry);
                }
                catch
                {
                    // 日志写入失败时静默处理，避免循环错误
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 关闭时取消是正常流程
        }
    }

    public void EnqueueLog(string logEntry)
    {
        try
        {
            if (_disposed || _logQueue.IsAddingCompleted)
            {
                return;
            }

            // 队列已满时 TryAdd 失败：丢弃该条并计数，避免日志风暴耗尽内存。
            if (!_logQueue.TryAdd(logEntry))
            {
                Interlocked.Increment(ref _droppedCount);
            }
        }
        catch (ObjectDisposedException)
        {
            // Dispose 竞态，安全忽略（须先于 InvalidOperationException：
            // ObjectDisposedException 派生自 InvalidOperationException，顺序颠倒会导致编译失败）
        }
        catch (InvalidOperationException)
        {
            // CompleteAdding 与 TryAdd 的竞态，安全忽略
        }
    }

    /// <summary>清理超过保留天数的日志文件（*.log），失败时静默忽略。</summary>
    private static void CleanupOldLogs(string? directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return;
        }

        var threshold = DateTime.Now.AddDays(-RetentionDays);
        foreach (var file in Directory.EnumerateFiles(directory, "*.log"))
        {
            try
            {
                if (File.GetLastWriteTime(file) < threshold)
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // 单个文件删除失败不影响启动
            }
        }
    }
}
