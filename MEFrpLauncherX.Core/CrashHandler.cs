using System.Globalization;

namespace MEFrpLauncherX.Core;

public class CrashHandler
{
    private static Exception? _ex;
    private static string? _path;

    private static readonly string[] _jokes =
    [
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
    ];

    private static readonly Dictionary<string, string> _resources = new()
    {
        {
            "zh-CN", """
                     =======================
                     = PML 2 崩溃报告 =
                     =======================
                     //{0}

                     时间: {1}
                     错误类型: {2}
                     描述: {3}
                     HResult: {18}

                     详细信息如下: 
                     -----------------------
                     == 主线程 ==
                     堆栈调用: 
                     {4}

                     == 内部错误 ==
                     是否有内部错误: {16}
                     {17}

                     == 系统信息 ==
                     软件版本: {5}
                     操作系统: {6}
                     启动参数: (合计{7}) {8}
                     正常运行时间: {9}s
                     文件路径: {11}
                     类型: {12}
                     当前语言: {13}
                     CPU: {15}{10}
                     """
        },
        {
            "zh-US", """
                     =======================
                     = PML 2 崩溃报告 =
                     =======================
                     //{0}

                     时间: {1}
                     错误类型: {2}
                     描述: {3}
                     HResult: {18}

                     详细信息如下: 
                     -----------------------
                     == 主线程 ==
                     堆栈调用: 
                     {4}

                     == 内部错误 ==
                     是否有内部错误: {16}
                     {17}

                     == 系统信息 ==
                     软件版本: {5}
                     操作系统: {6}
                     启动参数: (合计{7}) {8}
                     正常运行时间: {9}s
                     文件路径: {11}
                     类型: {12}
                     当前语言: {13}
                     CPU: {15}{10}
                     """
        },
        {
            "en-US", """
                     ============================
                     = PML 2 Crash Report =
                     ============================
                     //{0}

                     Crash Time: {1}
                     Error Type: {2}
                     Description: {3}
                     HResult: {18}

                     Details are below: 
                     -----------------------
                     == The Main Thread ==
                     StackTrace(s): 
                     {4}

                     == Inner Error ==
                     Has inner error: {16}
                     {17}

                     == System Infomation ==
                     Software Version: {5}
                     OS: {6}
                     Startup Parameters: (Total{7}) {8}
                     Uptime: {9}s
                     File Path: {11}
                     Type: {12}
                     Current Language: {13}
                     CPU Infomation: {15}{10}
                     """
        },
        {
            "ja-JP", """
                     ==================================
                     = PML 2 クラッシュ レポート =
                     ==================================
                     //{0}

                     クラッシュ時刻: {1}
                     エラータイプ: {2}
                     説明: {3}
                     HResult: {18}

                     詳細は以下の通りです: 
                     -----------------------
                     == メインスレッド ==
                     スタックトレース: 
                     {4}

                     == 内部エラー ==
                     内部エラーがある: {16}
                     {17}

                     == システム情報 ==
                     ソフトウェアバージョン: {5}
                     オペレーティング システム: {6}
                     起動パラメータ: （総数{7}）{8}
                     稼働時間: {9}s
                     ファイルパス: {11}
                     タイプ: {12}
                     現在の言語: {13}
                     CPU情報: {15}{10}
                     """
        },
        {
            "zh-TD", """
                     =======================
                     = PML 2 崩潰報告 =
                     =======================
                     //{0}

                     時間: {1}
                     錯誤類型: {2}
                     描述: {3}
                     HResult: {18}

                     詳細資訊如下: 
                     -----------------------
                     == 主線程 ==
                     堆疊追蹤: 
                     {4}

                     == 內部錯誤 ==
                     是否有內部錯誤: {16}
                     {17}

                     == 系統資訊 ==
                     軟件版本: {5}
                     操作系統: {6}
                     啟動參數: (合計{7}) {8}
                     正常運行時間: {9}s
                     文件路徑: {11}
                     類型: {12}
                     目前語言: {13}
                     CPU: {15}{10}
                     """
        },
    };

    private static readonly Dictionary<string, List<string>> _lang_res = new()
    {
        { "zh-CN", ["错误类型", "堆栈调用", "崩溃报告", "信息"] },
        { "zh-TD", ["錯誤類型", "堆疊調用", "崩潰報告"] },
        { "en-US", ["Error Type", "StackTrace(s)", "Crash Report", "Information: "] },
        { "ja-JP", ["エラーの種類", "スタック呼び出し", "クラッシュレポート"] },
    };

    private static readonly Dictionary<string, List<string>> _port_res = new()
    {
        { "zh-CN", ["客户端", "服务端"] },
        { "zh-TD", ["用戶端", "服務端"] },
        { "en-US", ["Client", "Server"] },
        { "ja-JP", ["クライアント", "サーバー"] },
    };

    public CrashHandler(Exception ex, string path)
    {
        _ex = ex;
        _path = path +
                $"\\{_lang_res[CultureInfo.CurrentCulture.Name][2]}_{DateTime.Now.Year}-{DateTime.Now.Month}-{DateTime.Now.Day}_{DateTime.Now.Hour}-{DateTime.Now.Minute}-{DateTime.Now.Second}+{DateTime.Now.Millisecond}.txt";
    }

    public void CollectCrashInfo()
    {
        var InnerExceptionProcess = _ex?.InnerException != null
            ? $"""
               {_lang_res["zh-CN"][0]}: {_ex.InnerException.GetType()}
               {_lang_res["zh-CN"][3]}: {_ex.InnerException.Message}
               HResult: {_ex.InnerException.HResult}
               {_lang_res["zh-CN"][1]}: 
               {_ex.InnerException.StackTrace}
               """
            : "";

        _resources["zh-CN"] = string.Format(_resources["zh-CN"],
            _jokes[new Random().Next(0, _jokes.Length - 1)],
            DateTime.Now.TimeOfDay,
            _ex?.GetType(),
            _ex?.Message,
            _ex?.StackTrace,
            Environment.OSVersion,
            "2.3.0", // 替换为实际版本号
            "0", // 启动参数数量
            "", // 启动参数
            "", // 正常运行时间
            "", // 额外信息
            AppDomain.CurrentDomain.BaseDirectory, // 文件路径
            _port_res["zh-CN"][0], // 类型
            CultureInfo.CurrentCulture.DisplayName,
            $"{GC.GetTotalMemory(false) / 1024 / 1024} MB", // 内存占用
            "",
            _ex?.InnerException != null,
            InnerExceptionProcess,
            _ex.HResult
        );
    }

    public bool WriteDumpFile()
    {
        try
        {
            File.WriteAllText(_path ?? AppDomain.CurrentDomain.BaseDirectory, _resources["zh-CN"]);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string GetCrashLog() => _resources["zh-CN"];
}