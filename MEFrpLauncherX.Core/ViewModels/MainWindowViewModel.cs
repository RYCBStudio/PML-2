using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ReactiveUI;

namespace MEFrpLauncherX.Core.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    // public InfoBarSeverity Type
// {
//     get;
//     set => this.RaiseAndSetIfChanged(ref field, value);
// }
//
// public string MsgTitle
// {
//     get;
//     set => this.RaiseAndSetIfChanged(ref field, value);
// }

    public MainWindowViewModel()
    {
        Instance = this;
    }

    public static MainWindowViewModel Instance
    {
        get;
        private set;
    }

    public string AppMessage
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public double Progress
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsDark
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = ConfigManager.CurrentConfig.Theme.Equals("dark", StringComparison.OrdinalIgnoreCase);

    public bool IsLoggedIn
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = false;
}

public class ThemeToBackgroundBrushConverter : IValueConverter
{
    public static ThemeToBackgroundBrushConverter Instance
    {
        get;
    } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true
            ? new SolidColorBrush(new Color(0x6F, 0x00, 0x00, 0x00))
            : new SolidColorBrush(new Color(0x6F, 0xFF, 0xFF, 0xFF));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}