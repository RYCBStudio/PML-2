using System;
using System.Globalization;

namespace MEFrpLauncherX.CrashDisplayer.ViewModels;

/// <summary>
///     轻量级多语言字符串（zh-CN 默认 / en-US / zh-Hant），按当前 UI 区域性取值
/// </summary>
public static class CrashStrings
{
    private static string Lang => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh"
        ? CultureInfo.CurrentUICulture.Name.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase) ||
          CultureInfo.CurrentUICulture.Name.Contains("-TW", StringComparison.OrdinalIgnoreCase) ||
          CultureInfo.CurrentUICulture.Name.Contains("-HK", StringComparison.OrdinalIgnoreCase) ||
          CultureInfo.CurrentUICulture.Name.Contains("-MO", StringComparison.OrdinalIgnoreCase)
            ? "hant"
            : "cn"
        : "en";

    public static string CrashTitle => Lang switch { "cn" => "程序崩溃报告", "hant" => "程式崩潰報告", _ => "Program Crash Report" };

    public static string OopsCrashed => Lang switch { "cn" => "哎呀，程序崩溃了！", "hant" => "哎呀，程式崩潰了！", _ => "Oops, the program crashed!" };

    public static string ProgrammerHumor => Lang switch { "cn" => "程序员幽默", "hant" => "程式設計師幽默", _ => "Programmer Humor" };

    public static string HumorNote => Lang switch
    {
        "cn" => "(此内容仅为缓解紧张情绪，与错误无关)",
        "hant" => "(此內容僅為緩解緊張情緒，與錯誤無關)",
        _ => "(This content is only to ease tension and is unrelated to the error)"
    };

    public static string ErrorDetailsLabel => Lang switch { "cn" => "错误详情:", "hant" => "錯誤詳情:", _ => "Error details:" };

    public static string CopyErrorInfo => Lang switch { "cn" => "复制错误信息", "hant" => "複製錯誤資訊", _ => "Copy error info" };

    public static string Close => Lang switch { "cn" => "关闭", "hant" => "關閉", _ => "Close" };

    public static string ErrorTypeLabel => Lang switch { "cn" => "错误类型", "hant" => "錯誤類型", _ => "Error type" };

    public static string ErrorMessageLabel => Lang switch { "cn" => "错误信息", "hant" => "錯誤資訊", _ => "Error message" };

    public static string SuggestionLabel => Lang switch { "cn" => "建议", "hant" => "建議", _ => "Suggestion" };

    public static string CheckAvailableMemory => Lang switch { "cn" => "检查可用内存", "hant" => "檢查可用記憶體", _ => "Check available memory" };

    public static string TryRestartProgram => Lang switch { "cn" => "尝试重启程序", "hant" => "嘗試重新啟動程式", _ => "Try restarting the program" };

    public static string[] Jokes => Lang switch
    {
        "cn" => JokesZhCn,
        "hant" => JokesZhHant,
        _ => JokesEnUs
    };

    private static readonly string[] JokesZhCn =
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

    private static readonly string[] JokesEnUs =
    {
        "We all have rough days.",
        "Rolling back power. Sit and relax.",
        "Something good is coming.",
        "Hello. Getting things ready for you.",
        "You're succeeding!",
        "Hey, long time no see!",
        "You're 30% done.",
        "Do! Bang! Cha-cha-cha Push-push.",
        "OneDrive: You have only 17179869184 GB of avaliable space",
        "F R E E  O F  C H A R G E",
        "Wondering how much battery is left? Wonder no more.",
        "Countdown to happiness.",
        "Windows 10 is not for all of us, but for each of us.",
        "You and your PC need to restart.",
        "Heads up.",
        "Windows arranged these settings to match your hardware performance.",
        "Terminology (in).",
        "Please do NOT™ turn off your computer.",
        "Microsoft Edge has a new look!",
        "Well, this is awkward.",
        "Malicious external devices are protected from attacks that protect your device's memory.",
        "Windows Sandbox is shutting down and will shut down.",
        "A bosom friend afar brings a distant land near.",
        "Rolling back to a previous version.",
        "100% fully charged.",
        "You look smart today!",
        "Don't blame us for not warning you.",
        "Heads up. To make sure you're up to date, we're going to update your Win10. Please do NOT™ turn off your computer. Sit and relax. You're succeeding.",
        "If the update fails, that's okay — we all have rough days. We suggest rolling back to a previous version, " +
        "or pressing Power to open a new kind of vegetable fuel " +
        "(BIOS) to configure it, then opening the Microsoft Edge browser to report it on Microsoft's official site, " +
        "or opening the Insider Hub to send feedback. We will review your feedback.",
        "You're succeeding! Heads up, the all-new Win11 is here! You can do exactly the same things in Win11, like bang, cha-cha-cha, push-push. You can share wonderful memory with your family, shared again and again.",
        "The all-new interface and split-screen features make Windows Tablet easy to use. Upgrading to Win11 is completely free of charge. " +
        "Just find the Insider Preview Program in the Insider Hub and click Upgrade; your PC will automatically roll back power. " +
        "Please sit and relax.",
        "You can always trust BugJump's update speed!",
        "Never Gonna Give the Minecraft Up",
        "Programmer: It works fine on my machine!",
        "It's not a bug, it's a feature!",
        "I swear I tested this code!",
        "User: Why did it crash? Program: That's a philosophical question.",
        "Error code: ID10T (eye-tracking error).",
        "A reboot fixes 99% of problems; the other 1% needs one more reboot.",
        "Our program just performed a free fall.",
        "It's not a crash, it's an artistic expression of unexpected termination.",
        "The error was successfully caught, but refused to be fixed.",
        "The program says it needs a break.",
        "Congratulations! You've discovered a hidden crash feature.",
        "This isn't the ending you wanted, but it's the ending the program chose.",
        "The program decided to clock out early.",
        "Error message: the programmer forgot to write error handling.",
        "It's not a crash, it's feature exploration mode."
    };

    private static readonly string[] JokesZhHant =
    {
        "我們都有不順利的時候。",
        "滾回功率，坐和放寬。",
        "好東西就要來了",
        "你好。正在為你作準備。",
        "你正在成功！",
        "嗨，別來無恙啊！",
        "你已完成30%",
        "做！轟！嚓-嚓-嚓 推-推",
        "OneDrive: You have only 17179869184 GB of avaliable space",
        "免 費 花 分 文",
        "想知道還剩下多少電量嗎？現在不必再想了。",
        "幸福倒數計時",
        "Windows 10 不是面向我們所有人，而是面向我們每一個人。",
        "您和您的電腦需要重新啟動。",
        "頭抬起",
        "Windows 整了這些設定以與你的硬體效能匹配",
        "術語(in)",
        "請勿™關閉電腦",
        "微軟邊緣有新面貌！",
        "這真是讓人尷尬",
        "惡意的外部設備免受攻擊保護你的設備的記憶體",
        "Windows沙盒正在關閉並將關閉",
        "海記憶體知己，天涯若比鄰",
        "滾回到以前的版本",
        "100%完全收費",
        "你今天看起來很聰明！",
        "不要怪我們沒有警告過你",
        "頭抬起，為了確保你是最新的，我們將要對你的窗10進行更新，請勿™關閉電腦，坐和放寬，你正在成功。",
        "如果更新失敗，沒關係，我們都有不順利的時候。建議你滾回到以前的版本，" +
        "或者按下功率 (Power)，打開一種新的植物性燃料 " +
        "(BIOS) 進行設定，然後可以打開微軟邊緣流量器 (Microsoft Edge)，進入微軟官網進行反映，" +
        "或者打開內部集線器 (Insider Hub) 進行回饋。我們會對你的回饋進行審批.",
        "您正在成功！ 頭抬起，全新的窗11來了！您可以在窗11中做完全一樣的事，如轟、嚓嚓嚓、推推。您可以和家人分享美妙的記憶體，分了又分。",
        "全新的介面和分屏功能使Windows Tablet使用便捷。升級到窗11完全免費花分文，" +
        "您只需要在內部集線器中找到預覽體驗計畫，並點擊升級，電腦會自動滾回功率。" +
        "請坐和放寬。",
        "你永遠可以相信BugJump的更新速度！",
        "Never Gonna Give the Minecraft Up",
        "程式設計師: 這在我的電腦上是好的啊！",
        "這不是bug，這是特性！",
        "我發誓我測試過這段代碼！",
        "使用者: 為什麼崩潰了？ 程式: 這是個哲學問題",
        "錯誤代碼: ID10T (眼動追蹤錯誤)",
        "重啟能解決99%的問題，剩下1%需要再重啟一次",
        "我們的程式剛剛表演了一次自由落體",
        "這不是崩潰，這是意外終止的藝術表現",
        "錯誤已成功捕獲，但拒絕被修復",
        "程式說它需要休息一下",
        "恭喜！你發現了一個隱藏的崩潰功能",
        "這不是你要的結局，但這是程式選擇的結局",
        "程式決定提前下班",
        "錯誤資訊: 程式設計師忘了寫錯誤處理",
        "這不是崩潰，這是功能探索模式"
    };
}
