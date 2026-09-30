using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace MEFrpLauncherX.Tools;

/// <summary>
///     AOT 诊断辅助：以文本形式导出可视树（类型/名称/尺寸/可见性/不透明度）。
///     用于定位「页面在 AOT 下渲染为空白」这类无法通过异常日志发现的问题。
/// </summary>
internal static class VisualTreeDiagnostics
{
    public static string Dump(Visual visual, int maxDepth = 6, int maxNodes = 150)
    {
        var sb = new StringBuilder();
        var count = 0;
        Walk(visual, sb, 0, ref count, maxDepth, maxNodes);
        return sb.ToString();
    }

    private static void Walk(Visual visual, StringBuilder sb, int depth, ref int count, int maxDepth, int maxNodes)
    {
        if (count >= maxNodes)
        {
            return;
        }

        count++;

        var ctrl = visual as Control;
        var bounds = ctrl?.Bounds ?? default;
        var boundsText = ctrl is null
            ? "(non-control)"
            : $"Bounds={bounds.Width:F0}x{bounds.Height:F0}";
        var visText = ctrl is null
            ? string.Empty
            : $" IsVisible={ctrl.IsVisible} Opacity={ctrl.Opacity:F2} Desired={ctrl.DesiredSize.Width:F0}x{ctrl.DesiredSize.Height:F0}";
        var nameText = string.IsNullOrEmpty(ctrl?.Name) ? string.Empty : $" '{ctrl!.Name}'";

        sb.Append(' ', depth * 2)
            .Append(visual.GetType().Name)
            .Append(nameText)
            .Append(' ')
            .Append(boundsText)
            .Append(visText)
            .AppendLine();

        if (depth >= maxDepth)
        {
            return;
        }

        foreach (var child in visual.GetVisualChildren())
        {
            Walk(child, sb, depth + 1, ref count, maxDepth, maxNodes);
        }
    }
}
