using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace MEFrpLauncherX.Core.WindowServices;

public static partial class ClickThroughHelper
{
    // --- Windows ---
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;

    public static void SetClickThrough(Window window, bool enable)
    {
        if (OperatingSystem.IsWindows())
        {
            SetClickThroughWindows(window, enable);
        }
        else if (OperatingSystem.IsMacOS())
        {
            SetClickThroughMac(window, enable);
        }
        else if (OperatingSystem.IsLinux())
        {
            SetClickThroughLinux(window, enable);
        }
    }

    // NativeAOT（LibraryImport）按精确导出名解析，不会像 DllImport 那样做 A/W 后缀回退。
    // user32.dll 没有 GetWindowLong/SetWindowLong 导出，只有 GetWindowLongW/SetWindowLongW（及 A/Ptr 变体），
    // 必须显式指定 EntryPoint；这里仅操作 32 位 GWL_EXSTYLE 样式值，无需 GetWindowLongPtrW。
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static partial int GetWindowLong(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static partial int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private static void SetClickThroughWindows(Window window, bool enable)
    {
        var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var exStyle = GetWindowLong(handle, GWL_EXSTYLE);
        if (enable)
        {
            exStyle |= WS_EX_TRANSPARENT | WS_EX_LAYERED;
        }
        else
        {
            exStyle &= ~WS_EX_TRANSPARENT;
        }

        SetWindowLong(handle, GWL_EXSTYLE, exStyle);
    }

    // --- macOS ---
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_getClass")]
    private static extern IntPtr objc_getClass(string className);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName")]
    private static extern IntPtr sel_registerName(string selectorName);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void_IntPtr_Bool(IntPtr receiver, IntPtr selector, bool value);

    private static void SetClickThroughMac(Window window, bool enable)
    {
        var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        // Get NSWindow* from window handle (Avalonia uses a wrapper, handle is usually NSWindow*)
        // Set [window setIgnoresMouseEvents: YES/NO]
        var sel = sel_registerName("setIgnoresMouseEvents:");
        objc_msgSend_void_IntPtr_Bool(handle, sel, enable);
    }
}