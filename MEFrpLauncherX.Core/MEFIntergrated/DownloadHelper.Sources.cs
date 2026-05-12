namespace MEFrpLauncherX.Core.MEFIntergrated;

public partial class DownloadHelper
{
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
        "Never Gonna Give the Minecraft Up"
    ];

    private readonly Dictionary<PlatformID, string> armDownloadUrls = new()
    {
        {
            PlatformID.Win32NT,
            "https://alist.yealqp.cn/download/mefrp-distributions/mefrpc-windows_arm64.exe"
        },
        {
            PlatformID.Unix,
            "https://alist.yealqp.cn/download/mefrp-distributions/mefrpc-linux_arm64.tar"
        },
        {
            PlatformID.MacOSX,
            "https://alist.yealqp.cn/download/mefrp-distributions/mefrpc-darwin_arm64.tar"
        }
    };

    private readonly Dictionary<PlatformID, string> downloadUrls = new()
    {
        {
            PlatformID.Win32NT,
            "https://alist.yealqp.cn/download/mefrp-distributions/mefrpc-windows_amd64.exe"
        },
        {
            PlatformID.Unix,
            "https://alist.yealqp.cn/download/mefrp-distributions/mefrpc-linux_amd64.tar"
        },
        {
            PlatformID.MacOSX,
            "https://alist.yealqp.cn/download/mefrp-distributions/mefrpc-darwin_amd64.tar"
        }
    };

    private readonly Dictionary<PlatformID, string> officialArmDownloadUrls = new()
    {
        {
            PlatformID.Win32NT,
            $"https://drive.mcsl.com.cn/d/ME-Frp/Lanzou/MEFrp-Core/{App.MEFrpVersion}/mefrpc_windows_arm64_{App.MEFrpVersion}.zip"
        },
        {
            PlatformID.Unix,
            $"https://drive.mcsl.com.cn/d/ME-Frp/Lanzou/MEFrp-Core/{App.MEFrpVersion}/mefrpc_linux_arm64_{App.MEFrpVersion}.tar"
        },
        {
            PlatformID.MacOSX,
            $"https://drive.mcsl.com.cn/d/ME-Frp/Lanzou/MEFrp-Core/{App.MEFrpVersion}/mefrpc_darwin_arm64_{App.MEFrpVersion}.tar"
        }
    };

    private readonly Dictionary<PlatformID, string> officialDownloadUrls = new()
    {
        {
            PlatformID.Win32NT,
            $"https://drive.mcsl.com.cn/d/ME-Frp/Lanzou/MEFrp-Core/{App.MEFrpVersion}/mefrpc_windows_amd64_{App.MEFrpVersion}.zip"
        },
        {
            PlatformID.Unix,
            $"https://drive.mcsl.com.cn/d/ME-Frp/Lanzou/MEFrp-Core/{App.MEFrpVersion}/mefrpc_linux_amd64_{App.MEFrpVersion}.tar"
        },
        {
            PlatformID.MacOSX,
            $"https://drive.mcsl.com.cn/d/ME-Frp/Lanzou/MEFrp-Core/{App.MEFrpVersion}/mefrpc_darwin_amd64_{App.MEFrpVersion}.tar"
        }
    };

    private string GetDownloadUrl(PlatformID platformId, bool isArm)
    {
        if (ConfigManager.CurrentConfig.DownloadSource.ToUpper() == "TPCA")
        {
            return isArm ? armDownloadUrls[platformId] : downloadUrls[platformId];
        }

        return isArm ? officialArmDownloadUrls[platformId] : officialDownloadUrls[platformId];
    }
}