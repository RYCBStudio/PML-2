param(
    [string] $AppPath,

    [double] $WarmupSeconds = 5,
    [double] $DurationSeconds = 8,
    [int] $MaxWidth = 640,
    [int] $MaxHeight = 360,
    [switch] $ClickNav
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($AppPath)) {
    throw 'AppPath is required.'
}

$native = @'
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class WindowFpsProbe
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;

    public static void ClickRelative(IntPtr hWnd, int relativeX, int relativeY)
    {
        RECT rect;
        if (!GetWindowRect(hWnd, out rect))
            return;

        SetCursorPos(rect.Left + relativeX, rect.Top + relativeY);
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
    }

    public static UInt64 CaptureHash(IntPtr hWnd, int maxWidth, int maxHeight)
    {
        RECT rect;
        if (!GetWindowRect(hWnd, out rect))
            return 0;

        int width = Math.Max(1, rect.Right - rect.Left);
        int height = Math.Max(1, rect.Bottom - rect.Top);
        int captureWidth = Math.Min(width, maxWidth);
        int captureHeight = Math.Min(height, maxHeight);
        int x = rect.Left + Math.Max(0, (width - captureWidth) / 2);
        int y = rect.Top + Math.Max(0, (height - captureHeight) / 2);

        using (Bitmap bitmap = new Bitmap(captureWidth, captureHeight, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(x, y, 0, 0, new Size(captureWidth, captureHeight), CopyPixelOperation.SourceCopy);

            Rectangle area = new Rectangle(0, 0, captureWidth, captureHeight);
            BitmapData data = bitmap.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                unchecked
                {
                    UInt64 hash = 14695981039346656037UL;
                    int stride = data.Stride;
                    int stepX = Math.Max(1, captureWidth / 160);
                    int stepY = Math.Max(1, captureHeight / 90);

                    byte[] buffer = new byte[Math.Abs(stride) * captureHeight];
                    Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

                    int strideAbs = Math.Abs(stride);
                    for (int row = 0; row < captureHeight; row += stepY)
                    {
                        int rowOffset = row * strideAbs;
                        for (int col = 0; col < captureWidth; col += stepX)
                        {
                            int offset = rowOffset + col * 4;
                            hash ^= buffer[offset + 0]; hash *= 1099511628211UL;
                            hash ^= buffer[offset + 1]; hash *= 1099511628211UL;
                            hash ^= buffer[offset + 2]; hash *= 1099511628211UL;
                        }
                    }

                    return hash;
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }
    }
}
'@

Add-Type -TypeDefinition $native -ReferencedAssemblies @('System.Drawing.dll')

if (!(Test-Path -LiteralPath $AppPath)) {
    throw "AppPath does not exist: $AppPath"
}

$workingDirectory = [System.IO.Path]::GetDirectoryName($AppPath)
$process = Start-Process -FilePath $AppPath -WorkingDirectory $workingDirectory -PassThru

$deadline = [DateTime]::UtcNow.AddSeconds(30)
$handle = [IntPtr]::Zero
while ([DateTime]::UtcNow -lt $deadline) {
    Start-Sleep -Milliseconds 250
    $process.Refresh()
    if ($process.HasExited) {
        throw "Process exited before a window appeared. ExitCode=$($process.ExitCode)"
    }
    if ($process.MainWindowHandle -ne 0) {
        $handle = [IntPtr]$process.MainWindowHandle
        break
    }
}

if ($handle -eq [IntPtr]::Zero) {
    throw "No main window appeared for PID $($process.Id)."
}

[WindowFpsProbe]::SetForegroundWindow($handle) | Out-Null
Start-Sleep -Seconds $WarmupSeconds

$stopwatch = [Diagnostics.Stopwatch]::StartNew()
$samples = New-Object System.Collections.Generic.List[double]
$changes = New-Object System.Collections.Generic.List[double]
$previous = [UInt64]0
$sampleCount = 0
$lastClickMs = -1000.0
$clickIndex = 0
$navPoints = @(
    @{ X = 40; Y = 255 },
    @{ X = 40; Y = 573 }
)

while ($stopwatch.Elapsed.TotalSeconds -lt $DurationSeconds) {
    if ($ClickNav -and ($stopwatch.Elapsed.TotalMilliseconds - $lastClickMs) -ge 900) {
        $point = $navPoints[$clickIndex % $navPoints.Count]
        [WindowFpsProbe]::ClickRelative($handle, $point.X, $point.Y)
        $clickIndex++
        $lastClickMs = $stopwatch.Elapsed.TotalMilliseconds
    }

    $hash = [WindowFpsProbe]::CaptureHash($handle, $MaxWidth, $MaxHeight)
    $now = $stopwatch.Elapsed.TotalMilliseconds
    $samples.Add($now)
    if ($sampleCount -gt 0 -and $hash -ne $previous) {
        $changes.Add($now)
    }
    $previous = $hash
    $sampleCount++
}

$elapsedSeconds = $stopwatch.Elapsed.TotalSeconds
$intervals = New-Object System.Collections.Generic.List[double]
for ($i = 1; $i -lt $changes.Count; $i++) {
    $intervals.Add($changes[$i] - $changes[$i - 1])
}

function Get-Percentile([double[]] $Values, [double] $Percentile) {
    if ($Values.Length -eq 0) { return $null }
    $sorted = $Values | Sort-Object
    $index = [Math]::Min($sorted.Length - 1, [Math]::Max(0, [int][Math]::Round(($sorted.Length - 1) * $Percentile)))
    return $sorted[$index]
}

$intervalArray = [double[]]$intervals.ToArray()
$result = [ordered]@{
    app = $AppPath
    pid = $process.Id
    samples = $sampleCount
    sampleRate = [Math]::Round($sampleCount / $elapsedSeconds, 2)
    visibleChanges = $changes.Count
    visibleChangeRate = [Math]::Round($changes.Count / $elapsedSeconds, 2)
    medianChangeMs = if ($intervalArray.Length -gt 0) { [Math]::Round((Get-Percentile $intervalArray 0.50), 2) } else { $null }
    p95ChangeMs = if ($intervalArray.Length -gt 0) { [Math]::Round((Get-Percentile $intervalArray 0.95), 2) } else { $null }
    minChangeMs = if ($intervalArray.Length -gt 0) { [Math]::Round(($intervalArray | Measure-Object -Minimum).Minimum, 2) } else { $null }
    maxChangeMs = if ($intervalArray.Length -gt 0) { [Math]::Round(($intervalArray | Measure-Object -Maximum).Maximum, 2) } else { $null }
}

try {
    $process.CloseMainWindow() | Out-Null
    Start-Sleep -Seconds 2
    $process.Refresh()
    if (!$process.HasExited) {
        Stop-Process -Id $process.Id -Force
    }
}
catch {
    try { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue } catch {}
}

$result | ConvertTo-Json -Depth 3
