using Avalonia.Media.Imaging;
using FeatherQR;
using FeatherQR.SkiaSharp;
using SkiaSharp;

namespace MEFrpLauncherX.Core.Services;

public sealed class QRCodeService
{
    public static byte[] GetQRCodeBytesArray(string text, ECCLevel eccLevel = ECCLevel.M, int size = 512,
        SKColor? foreground = null, SKColor? background = null)
    {
        return new QRCodeImageBuilder(text)
            .WithErrorCorrection(eccLevel)
            .WithSize(size, size)
            .WithColors(foreground ?? SKColors.Black, background ?? SKColors.Transparent)
            .ToByteArray();
    }

    public static Bitmap GetQRCodeBitmap(string text, ECCLevel eccLevel = ECCLevel.M, int size = 512,
        SKColor? foreground = null, SKColor? background = null)
    {
        return new Bitmap(new MemoryStream(GetQRCodeBytesArray(text, eccLevel, size, foreground, background)));
    }
}