using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Styling;
using MEFrpLauncherX.Core;
using ReactiveUI;

namespace MEFrpLauncherX.ViewModels;

public class OpenSourceLibrary
{
    public string Name
    {
        get;
        set;
    } = string.Empty;

    public string Url
    {
        get;
        set;
    } = string.Empty;

    public string License
    {
        get;
        set;
    } = string.Empty;
}

public class AboutViewModel : ViewModelBase
{
    public string? Hitokoto
    {
        get => field.IsNullOrEmpty() ? string.Empty : $"{field}";
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string? From
    {
        get => field.IsNullOrEmpty() ? string.Empty : $"「{field}」";
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string? Author
    {
        get => field.IsNullOrEmpty() ? string.Empty : $"  —— {field}";
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsDark => Application.Current?.ActualThemeVariant == ThemeVariant.Dark;

    public ObservableCollection<OpenSourceLibrary> OpenSourceLibraries
    {
        get;
    } =
    [
        new() { Name = "Avalonia UI", Url = "https://github.com/AvaloniaUI/Avalonia", License = "MIT" },
        new() { Name = "FluentAvaloniaUI", Url = "https://github.com/amwx/FluentAvalonia", License = "MIT" },
        new() { Name = "ReactiveUI", Url = "https://github.com/reactiveui/ReactiveUI", License = "MIT" },
        new() { Name = "LiveCharts2", Url = "https://github.com/beto-rodriguez/LiveCharts2", License = "MIT" },
        new()
        {
            Name = "MessageBox.Avalonia", Url = "https://github.com/AvaloniaCommunity/MessageBox.Avalonia",
            License = "MIT"
        },
        new() { Name = "AvaloniaEdit", Url = "https://github.com/AvaloniaUI/AvaloniaEdit", License = "MIT" },
        new() { Name = "IconPacks", Url = "https://github.com/MahApps/IconPacks", License = "MIT" },
        new()
        {
            Name = "AsyncImageLoader.Avalonia", Url = "https://github.com/AvaloniaUtils/AsyncImageLoader.Avalonia",
            License = "MIT"
        },
        new() { Name = "Avalonia.Svg", Url = "https://github.com/wieslawsoltes/Svg.Skia", License = "MIT" },
        new() { Name = "Downloader", Url = "https://github.com/bezzad/Downloader", License = "MIT" },
        new() { Name = "RestSharp", Url = "https://github.com/restsharp/RestSharp", License = "Apache-2.0" },
        new() { Name = "SkiaSharp", Url = "https://github.com/mono/SkiaSharp", License = "MIT" },
        new() { Name = "Sentry", Url = "https://github.com/getsentry/sentry-dotnet", License = "MIT" },
        new() { Name = "YamlDotNet", Url = "https://github.com/aaubry/YamlDotNet", License = "MIT" },
        new() { Name = "Tomlyn", Url = "https://github.com/xoofx/Tomlyn", License = "BSD-2-Clause" },
        new() { Name = "NPinyin.Core", Url = "https://github.com/lxconan/NPinyin", License = "MIT" },
        new()
        {
            Name = "Microsoft PinYinConverter",
            Url = "https://www.nuget.org/packages/Microsoft.International.Converters.PinYinConverter",
            License = "Microsoft"
        },
        new() { Name = "TextMateSharp", Url = "https://github.com/danipen/TextMateSharp", License = "MIT" },
        new() { Name = "Svg.Model", Url = "https://github.com/wieslawsoltes/Svg", License = "MIT" },
    ];

    public bool IsSubmittingFeedback
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public double SubmitProgress
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }
}