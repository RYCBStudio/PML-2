using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;

namespace MEFrpLauncherX.Controls;

public partial class UserProxyControl : UserControl
{
    public static UserProxyControl Instance;
    private CancellationTokenSource _downloadCancellationTokenSource;

    public UserProxyControl()
    {
        InitializeComponent();
        Instance = this;
    }

    private void ShowMoreOptions(object? sender, RoutedEventArgs e) => ShowMenu();

    private void HideMoreOptions(object? sender, ContextRequestedEventArgs e)
    {
        ShowMenu();
        e.Handled = true;
    }

    private void ShowMenu()
    {
        var flyout = Resources["MoreOptionsCmd"] as CommandBarFlyout;
        flyout?.ShowMode = FlyoutShowMode.Standard;

        flyout?.ShowAt(MoreOptionsBtn);
    }

    private void LaunchWeb(object? sender, RoutedEventArgs e)
    {
        var toOpen = (sender as ContentControl)?.Content?.ToString();
        toOpen = TypeIdentifier.Text switch
        {
            "HTTP" => $"http://{toOpen}",
            "HTTPS" => $"https://{toOpen}",
            _ => toOpen
        };
        Core.App.MainWindow?.Launcher.LaunchUriAsync(new Uri(toOpen));
    }
}

public class OnlineAndDisabledConverter : IMultiValueConverter
{
    public static OnlineAndDisabledConverter Instance
    {
        get;
    } = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count != 2)
        {
            return Application.Current.TryGetResource("SystemFillColorCriticalBrush", Application.Current.ActualThemeVariant, out var o)
                ? o
                : null;
        }

        var isOnline = values[0] as bool?;
        var isDisabled = values[1] as bool?;
        if (isOnline == true)
        {
            return Application.Current.TryGetResource("SystemFillColorSuccessBrush", Application.Current.ActualThemeVariant, out var o)
                ? o
                : null;
        }

        if (isOnline == false && isDisabled == true)
        {
            return Application.Current.TryGetResource("SystemFillColorCautionBrush", Application.Current.ActualThemeVariant, out var o)
                ? o
                : null;
        }

        if (isOnline == false && isDisabled == false)
        {
            return Application.Current.TryGetResource("SystemFillColorCriticalBrush", Application.Current.ActualThemeVariant, out var o)
                ? o
                : null;
        }

        return Application.Current.TryGetResource("SystemFillColorAttentionBrush", Application.Current.ActualThemeVariant, out var o1)
            ? o1
            : null;
    }
}