using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using RYCB.PML2.Splash.ViewModels;

namespace RYCB.PML2.Splash.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(string[]? args)
    {
        InitializeComponent();
        ViewModel = new MainWindowViewModel();
        DataContext = ViewModel;
        if (args != null)
        {
            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--version" or "-v" when i + 1 < args.Length:
                        TbVersion.Text = args[i + 1];
                        i++;
                        break;
                    case "--back" or "-b" or "--background" when i + 1 < args.Length:
                        ImgBackground.Source = new Bitmap(args[i + 1]);
                        i++;
                        break;
                    // 26.3.1 M2：启动画面样式（default / dark / minimal），配合背景图调整前景与衬底
                    case "--style" when i + 1 < args.Length:
                        ApplyStyle(args[i + 1]);
                        i++;
                        break;
                    // 26.3.1 M1：主程序进度管道（tech.rycb.pml2.splash.{pid}）
                    case "--pipe" when i + 1 < args.Length:
                        StartPipeListener(args[i + 1]);
                        i++;
                        break;
                }
            }
        }
    }

    /// <summary>按样式调整前景色 / 内容层衬底，与背景图风格一致（26.3.1 M2）</summary>
    private void ApplyStyle(string style)
    {
        switch (style)
        {
            case "dark":
                // 深色背景图：白字（axaml 默认），进度条用主题蓝提升对比
                PbProgress.Foreground = new SolidColorBrush(Color.Parse("#4C9AFF"));
                break;
            case "minimal":
                // 浅色背景图：深色文字 + 浅色衬底 + 深色进度条
                ContentBorder.Background = new SolidColorBrush(Color.Parse("#59FFFFFF"));
                TbTitle.Foreground = new SolidColorBrush(Color.Parse("#1F2933"));
                TbVersion.Foreground = new SolidColorBrush(Color.Parse("#52606D"));
                TbBrand.Foreground = new SolidColorBrush(Color.Parse("#3E4C59"));
                TbStatus.Foreground = new SolidColorBrush(Color.Parse("#1F2933"));
                PbProgress.Foreground = new SolidColorBrush(Color.Parse("#4C9AFF"));
                break;
            // default：保持 axaml 默认（白字 + 半透明黑衬底）
        }
    }

    /// <summary>进度管道消息类型（与主程序协议一致：progress / done / error）</summary>
    private sealed record PipeMessage(string Type, double Percent, string? Message);

    private MainWindowViewModel? ViewModel;

    /// <summary>
    ///     监听主程序进度管道：一行一条 UTF-8 JSON。
    ///     progress → 更新进度与文案；done / error → 关闭本窗口（Splash 结束）。
    ///     管道连接失败/主程序不发消息时保持现状（超时由主程序 Kill 兜底）。
    /// </summary>
    private void StartPipeListener(string pipeName)
    {
        _ = Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(
                        pipeName,
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync();
                    using var reader = new StreamReader(server, Encoding.UTF8);
                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        if (!TryApplyMessage(line))
                        {
                            return; // done / error：结束监听并关闭
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (IOException)
                {
                    // 客户端断开，继续等待下一次连接
                }
                catch
                {
                    return;
                }
            }
        });
    }

    private bool TryApplyMessage(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
            switch (type)
            {
                case "progress":
                {
                    var percent = root.TryGetProperty("percent", out var p) ? p.GetDouble() : 0;
                    var message = root.TryGetProperty("message", out var m) ? m.GetString() : null;
                    Dispatcher.UIThread.Post(() => ViewModel?.UpdateProgress(percent, message));
                    return true;
                }
                case "done":
                    Dispatcher.UIThread.Post(Close);
                    return false;
                case "error":
                {
                    var message = root.TryGetProperty("message", out var m) ? m.GetString() : null;
                    Dispatcher.UIThread.Post(() => ViewModel?.ShowError(message));
                    return false;
                }
            }
        }
        catch
        {
            // 忽略无法解析的行
        }

        return true;
    }

    private bool _mouseDownForWindowMoving = false;
    private PointerPoint _originalPoint;

    private void InputElement_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_mouseDownForWindowMoving) return;

        PointerPoint currentPoint = e.GetCurrentPoint(this);
        Position = new PixelPoint(Position.X + (int)(currentPoint.Position.X - _originalPoint.Position.X),
            Position.Y + (int)(currentPoint.Position.Y - _originalPoint.Position.Y));
    }

    private void InputElement_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (WindowState == WindowState.Maximized || WindowState == WindowState.FullScreen) return;

        _mouseDownForWindowMoving = true;
        _originalPoint = e.GetCurrentPoint(this);
    }

    private void InputElement_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _mouseDownForWindowMoving = false;
    }
}
