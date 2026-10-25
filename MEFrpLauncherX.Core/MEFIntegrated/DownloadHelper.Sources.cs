namespace MEFrpLauncherX.Core.MEFIntegrated;

public partial class DownloadHelper
{
    private static readonly string[] _jokes =
        Languages.Languages.Text_Download_Jokes.Split('\n', StringSplitOptions.RemoveEmptyEntries);

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

    private readonly Dictionary<PlatformID, string> armRetryDownloadUrls = new()
    {
        {
            PlatformID.Win32NT,
            "https://alist.yealqp.cn/download/mefrp-distributions/mefrpc_windows_arm64.exe"
        },
        {
            PlatformID.Unix,
            "https://alist.yealqp.cn/download/mefrp-distributions/mefrpc_linux_arm64.tar"
        },
        {
            PlatformID.MacOSX,
            "https://alist.yealqp.cn/download/mefrp-distributions/mefrpc_darwin_arm64.tar"
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

    private readonly Dictionary<PlatformID, string> downloadRetryUrls = new()
    {
        {
            PlatformID.Win32NT,
            "https://alist.yealqp.cn/download/mefrp-distributions/mefrpc_windows_amd64.exe"
        },
        {
            PlatformID.Unix,
            "https://alist.yealqp.cn/download/mefrp-distributions/mefrpc_linux_amd64.tar"
        },
        {
            PlatformID.MacOSX,
            "https://alist.yealqp.cn/download/mefrp-distributions/mefrpc_darwin_amd64.tar"
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
        if (IsTpcaSource())
        {
            return isArm ? armDownloadUrls[platformId] : downloadUrls[platformId];
        }

        return isArm ? officialArmDownloadUrls[platformId] : officialDownloadUrls[platformId];
    }

    /// <summary>当前设置中是否选择了 TPCA 下载源。</summary>
    private static bool IsTpcaSource() =>
        ConfigManager.CurrentConfig.DownloadSource.Equals("TPCA", StringComparison.CurrentCultureIgnoreCase);

    /// <summary>
    ///     备用下载地址（重试时切源用）。
    ///     <para>
    ///         仅 TPCA 源存在备用命名版本（<c>downloadRetryUrls</c> / <c>armRetryDownloadUrls</c>，
    ///         与主地址同一主机、同为直接可用的 exe/tar）；
    ///         官方源没有备用地址，返回 <see langword="null" />，由调用方沿用主地址 —— 不虚构不存在的下载源。
    ///     </para>
    /// </summary>
    private string? GetFallbackDownloadUrl(PlatformID platformId, bool isArm) =>
        IsTpcaSource()
            ? isArm ? armRetryDownloadUrls[platformId] : downloadRetryUrls[platformId]
            : null;
}