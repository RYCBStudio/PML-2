using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace MEFrpLauncherX.Core;

public static partial class ClickThroughHelper
{
    // X11 shape constants
    private const int ShapeInput = 2;
    private const int ShapeSet = 0;

    // --- Linux (X11, basic version) ---
    // This is not guaranteed to work everywhere and is only a best-effort.
    private static void SetClickThroughLinux(Window window, bool enable)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("This Linux implementation only works on Linux/X11.");
        }

        var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Could not get X11 window handle.");
        }

        var display = Xlib.XOpenDisplay(IntPtr.Zero);
        if (display == IntPtr.Zero)
        {
            throw new InvalidOperationException("Could not open X11 display.");
        }

        try
        {
            if (enable)
            {
                // Set empty input shape: click-through
                XShapeCombineRectangles(
                    display, handle, ShapeInput, 0, 0, IntPtr.Zero, 0, ShapeSet, 0
                );
            }
            else
            {
                // Reset input shape: normal input
                XShapeCombineMask(
                    display, handle, ShapeInput, 0, 0, IntPtr.Zero, ShapeSet
                );
            }

            Xlib.XFlush(display);
        }
        finally
        {
            Xlib.XCloseDisplay(display);
        }
    }

    // X11 and XShape interop

    [DllImport("libX11", EntryPoint = "XOpenDisplay")]
    public static extern IntPtr XOpenDisplay(IntPtr display);

    [DllImport("libX11", EntryPoint = "XCloseDisplay")]
    public static extern int XCloseDisplay(IntPtr display);

    [DllImport("libX11", EntryPoint = "XFlush")]
    public static extern int XFlush(IntPtr display);

    [DllImport("libXext", EntryPoint = "XShapeCombineRectangles")]
    public static extern void XShapeCombineRectangles(
        IntPtr display, IntPtr window, int shape, int x, int y,
        IntPtr rectangles, int n_rects, int op, int ordering);

    [DllImport("libXext", EntryPoint = "XShapeCombineMask")]
    public static extern void XShapeCombineMask(
        IntPtr display, IntPtr window, int shape, int x, int y,
        IntPtr mask, int op);
}

// For Xlib calls
internal static class Xlib
{
    [DllImport("libX11", EntryPoint = "XOpenDisplay")]
    public static extern IntPtr XOpenDisplay(IntPtr display);

    [DllImport("libX11", EntryPoint = "XCloseDisplay")]
    public static extern int XCloseDisplay(IntPtr display);

    [DllImport("libX11", EntryPoint = "XFlush")]
    public static extern int XFlush(IntPtr display);
}