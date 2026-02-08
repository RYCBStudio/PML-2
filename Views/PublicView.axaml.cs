using System;
using Avalonia.Controls;
using MEFrpLauncherX.Core.MEFIntergrated;

namespace MEFrpLauncherX.Views;

public partial class PublicView : UserControl
{
    public PublicView()
    {
        InitializeComponent();
        this.Loaded += (s, e) =>
        {
            MEFApiConverter.Initialize();
            Node.TargetNumber = MEFApiConverter.CurrentPublicInfo.data?.nodes ?? 0;
            Users.TargetNumber = MEFApiConverter.CurrentPublicInfo.data?.users ?? 0;
            Proxies.TargetNumber = MEFApiConverter.CurrentPublicInfo.data?.proxies ?? 0;
            Traffic.TargetNumber = ProcessFileSize(MEFApiConverter.CurrentPublicInfo.data?.traffic ?? 0);
        };
    }

    /// <summary>
    /// 根据<paramref name="fileSize"/>的大小自动返回对应的文件大小值。
    /// <br/>
    /// 如：若<paramref name="fileSize"/>32743879328,则返回30.50GB；
    /// 返回值的数值范围为1~1000。
    /// </summary>
    /// <param name="fileSize">文件大小，单位为Bytes</param>
    /// <returns>处理后的文件大小值。</returns>
    private static int ProcessFileSize(long fileSize)
    {
        string[] sizeUnits = ["B", "KB", "MB", "GB", "TB"];
        double size = fileSize;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < sizeUnits.Length - 1)
        {
            size /= 1024;
        }

        return Convert.ToInt32(Math.Round(size));
    }
}