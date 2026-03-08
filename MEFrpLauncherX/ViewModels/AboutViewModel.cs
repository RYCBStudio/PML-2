using Avalonia;
using Avalonia.Styling;
using MEFrpLauncherX.Core;
using ReactiveUI;

namespace MEFrpLauncherX.ViewModels;

public class AboutViewModel : ViewModelBase
{
    public string? Hitokoto
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string? From
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string? Author
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsDark => Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
}