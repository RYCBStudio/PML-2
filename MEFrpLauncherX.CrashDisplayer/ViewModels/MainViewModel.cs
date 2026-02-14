using System;
using System.Text;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace MEFrpLauncherX.CrashDisplayer.ViewModels;

public partial class MainViewModel
{
    public string JokeMessage { get; set; }
    public string ErrorSummary { get; set; }
    public string ErrorDetails { get; set; }
    public ICommand CopyCommand { get; set; }
    public ICommand CloseCommand { get; set; }

    public MainViewModel(string exJson, string crashLogEncrypted)
    {
        // 幽默消息
        var jokes = new[]
        {
            "我们都有不顺利的时候。",
            "滚回功率，坐和放宽。",
            "好东西就要来了",
            "你好。正在为你作准备。",
            "你正在成功！",
            "嗨，别来无恙啊！",
            "你已完成30%",
            "做！轰！嚓-嚓-嚓 推-推",
            "OneDrive: You have only 17179869184 GB of avaliable space",
            "免 费 花 分 文",
            "想知道还剩下多少电量吗？现在不必再想了。",
            "幸福倒计时",
            "Windows 10 不是面向我们所有人，而是面向我们每一个人。",
            "您和您的电脑需要重新启动。",
            "头抬起",
            "Windows 整了这些设置以与你的硬件性能匹配",
            "术语(in)",
            "请勿™关闭计算机",
            "微软边缘有新面貌！",
            "这真是让人尴尬",
            "恶意的外部设备免受攻击保护你的设备的内存",
            "Windows沙盒正在关闭并将关闭",
            "海記憶體知己，天涯若比鄰",
            "滚回到以前的版本",
            "100%完全收费",
            "你今天看起来很聪明！",
            "不要怪我们没有警告过你",
            "头抬起，为了确保你是最新的，我们将要对你的窗10进行更新，请勿™关闭计算机，坐和放宽，你正在成功。",
            "如果更新失败，没关系，我们都有不顺利的时候。建议你滚回到以前的版本，" +
            "或者按下功率 (Power)，打开一种新的植物性燃料 " +
            "(BIOS) 进行设置，然后可以打开微软边缘流量器 (Microsoft Edge)，进入微软官网进行反映，" +
            "或者打开内部集线器 (Insider Hub) 进行反馈。我们会对你的反馈进行审批.",
            "您正在成功！ 头抬起，全新的窗11来了！您可以在窗11中做完全一样的事，如轰、嚓嚓嚓、推推。您可以和家人分享美妙的内存，分了又分。",
            "全新的界面和分屏功能使Windows Tablet使用便捷。升级到窗11完全免费花分文，" +
            "您只需要在内部集线器中找到预览体验计划，并点击升级，电脑会自动滚回功率。" +
            "请坐和放宽。",
            "你永远可以相信BugJump的更新速度！",
            "Never Gonna Give the Minecraft Up",
            "程序员: 这在我的电脑上是好的啊！",
            "这不是bug，这是特性！",
            "我发誓我测试过这段代码！",
            "用户: 为什么崩溃了？ 程序: 这是个哲学问题",
            "错误代码: ID10T (眼动追踪错误)",
            "重启能解决99%的问题，剩下1%需要再重启一次",
            "我们的程序刚刚表演了一次自由落体",
            "这不是崩溃，这是意外终止的艺术表现",
            "错误已成功捕获，但拒绝被修复",
            "程序说它需要休息一下",
            "恭喜！你发现了一个隐藏的崩溃功能",
            "这不是你要的结局，但这是程序选择的结局",
            "程序决定提前下班",
            "错误信息: 程序员忘了写错误处理",
            "这不是崩溃，这是功能探索模式"
        };

        var random = new Random();
        JokeMessage = jokes[random.Next(jokes.Length)];
        var exInfo = Base64Decode(exJson).Split("||");
        var ex = new ExceptionInfo
        {
            Type = exInfo[0],
            Message = exInfo[1],
            StackTrace = exInfo[2]
        };
        if (ex.Type.Contains("QuicException")) Environment.Exit(0);
        var crashLog = Base64Decode(crashLogEncrypted);
        // 错误摘要
        ErrorSummary = $"错误类型: {ex.Type}\n\n" +
                       $"错误信息: {ex.Message}\n\n" +
                       $"建议: {(ex.Message.Contains("内存")||ex.Message.Contains("Memory") ? "检查可用内存" : "尝试重启程序")}";

        // 错误详情
        ErrorDetails = crashLog;

        // 命令
        CopyCommand = new RelayCommand(_ =>
            (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime).MainWindow.Clipboard
            .SetTextAsync(crashLog));
        CloseCommand = new RelayCommand(_ =>
            (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime).Shutdown());
    }
    
    public static string Base64Decode(string base64EncodedData)
    {
        var base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
        return Encoding.UTF8.GetString(base64EncodedBytes);
    }
}

public class ExceptionInfo
{
    public string Type { get; set; }
    public string Message { get; set; }
    public string StackTrace { get; set; }

    public override string ToString()
    {
        return $"{Type}: {Message}{Environment.NewLine}{StackTrace}";
    }
}

public class RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
    : ICommand
{
    private readonly Action<object> _execute = execute ?? throw new ArgumentNullException(nameof(execute));

    public bool CanExecute(object parameter) => canExecute?.Invoke(parameter) ?? true;

    public void Execute(object parameter) => _execute(parameter);

    public event EventHandler CanExecuteChanged;
}