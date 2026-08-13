namespace MEFrpLauncherX.Core.Languages;

public sealed class DailyTips
{
    public Dictionary<string, IReadOnlyList<string>>? DailyTipsLocalization
    {
        get;
    }

    private List<string> DailyTips_zh_CN =
    [
        """
        在登录页面时, 您可以先点击"以后自动登录"开关, 再输入用户名, 这样就不必再输入密码了。
        """,
        """
        在主页, 您可以将鼠标悬停在"剩余流量"上, 查看以GB为单位的剩余流量。
        """,
        "PML 2 支持显示的最高流量单位为 **YB** !",
        "当您选择用配置文件启动隧道时, PML 2 会自动索引软件配置目录下的所有符合条件的配置文件! (*由于 macOS 的限制, 此功能目前**仅适用于 Windows 和 Linux**)",
        "当您觉得 PML 2 不错时, 请考虑[给它一个 Star](https://github.com/RYCBStudio/PML-2)哦!",
    ];
}