using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MEFrpLauncherX.Core;

namespace MEFrpLauncherX.Services;

/// <summary>
///     Splash 进度管道消息（26.3.1 M1）：与独立 Splash 进程约定的 JSON 行协议。
///     <c>type</c>: progress / done / error。
/// </summary>
public sealed record SplashPipeMessage(string Type, double Percent, string? Message);

/// <summary>
///     Splash 进度服务：把主程序启动阶段的 <see cref="ISplashService.UpdateProgress" /> 通过
///     NamedPipe（tech.rycb.pml2.splash.{pid}）推送到独立 Splash 进程。
///     与单实例激活管道 <c>tech.rycb.pml2</c>（SHOW）严格分离；管道失败不影响启动。
/// </summary>
public class PipeSplashService : ISplashService
{
    private readonly string _pipeName;
    private readonly object _sync = new();

    public PipeSplashService(string pipeName) => _pipeName = pipeName;

    /// <summary>独立 Splash 已由 Program.Main 启动，无需额外展示</summary>
    public void Show()
    {
    }

    public void UpdateProgress(double progress, string progressText)
    {
        SendMessage(new SplashPipeMessage("progress", Math.Clamp(progress, 0, 100), progressText));
    }

    public void Close()
    {
        SendMessage(new SplashPipeMessage("done", 100, null));
    }

    private void SendMessage(SplashPipeMessage message)
    {
        lock (_sync)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
                client.Connect(500);
                using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
                writer.WriteLine(JsonSerializer.Serialize(message, App.AppJsonSerializerContext.SplashPipeMessage));
                if (OperatingSystem.IsWindows())
                {
                    client.WaitForPipeDrain();
                }
            }
            catch
            {
                // 管道失败不影响启动：Splash 保持静态画面，主程序正常进入主窗
            }
        }
    }
}
