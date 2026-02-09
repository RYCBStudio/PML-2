using System.Collections.Concurrent;

namespace MEFrpLauncherX.Core;

public class AsyncLogWriter : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly BlockingCollection<string> _logQueue = new();
    private readonly Task _processingTask;
    private readonly CancellationTokenSource _cts = new();

    public AsyncLogWriter(string filePath)
    {
        // 确保日志目录存在
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        _writer = new StreamWriter(new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
        {
            AutoFlush = true,
        };

        _processingTask = Task.Run(ProcessLogQueue);
    }

    private async Task ProcessLogQueue()
    {
        foreach (var logEntry in _logQueue.GetConsumingEnumerable(_cts.Token))
        {
            try
            {
                await _writer.WriteLineAsync(logEntry);
            }
            catch
            {
                // 日志写入失败时静默处理，避免循环错误
            }
        }
    }

    public void EnqueueLog(string logEntry)
    {
        if (!_logQueue.IsAddingCompleted)
        {
            _logQueue.Add(logEntry);
        }
    }

    public void Dispose()
    {
        _logQueue.CompleteAdding();
        _cts.Cancel();
        try
        {
            _processingTask.Wait(TimeSpan.FromSeconds(5));
        }catch(Exception){}

        _writer.Dispose();
        _logQueue.Dispose();
        _cts.Dispose();

        GC.SuppressFinalize(this);
    }
}