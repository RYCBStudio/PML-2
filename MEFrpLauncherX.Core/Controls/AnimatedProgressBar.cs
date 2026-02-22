using Avalonia;
using Avalonia.Controls;

namespace MEFrpLauncherX.Core.Controls;

public class AnimatedProgressBar : ProgressBar
{
    protected async override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        var value = Value;
        for (var i = 0; i < value; i++)
        {
            Value = i;
            await Task.Delay(TimeSpan.FromMilliseconds(10));
        }
    }
}