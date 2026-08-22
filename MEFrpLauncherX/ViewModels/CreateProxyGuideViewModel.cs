using System;
using System.Globalization;
using Avalonia.Collections;
using Avalonia.Data.Converters;
using IconPacks.Avalonia.FileIcons;
using IconPacks.Avalonia.Lucide;
using IconPacks.Avalonia.Material;
using IconPacks.Avalonia.SimpleIcons;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Languages;
using ReactiveUI;

namespace MEFrpLauncherX.ViewModels;

public class CreateProxyGuideViewModel : ViewModelBase
{
    public AvaloniaList<ProxyType> AvailableTypes
    {
        get;
        set;
    }

    public ProxyType? SelectedType
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public CreateProxyGuideViewModel()
    {
        AvailableTypes =
        [
            new ProxyType()
            {
                Name = "AList/OpenList",
                Description = Languages.Text_CreateProxyGuide_AListDesc,
                Icon = new PackIcon()
                {
                    Icon = "AList",
                    IconFontFamily = "SimpleIcons"
                },
                Category = "Web"
            },
            new ProxyType()
            {
                Name = "Minecraft Java Edition",
                Description = Languages.Text_CreateProxyGuide_MinecraftJavaDesc,
                Icon = new PackIcon()
                {
                    Icon = "Minecraft",
                    IconFontFamily = "FileIcons"
                },
                Category = "Game"
            },
            new ProxyType()
            {
                Name = "Minecraft Bedrock Edition",
                Description = Languages.Text_CreateProxyGuide_MinecraftBedrockDesc,
                Icon = new PackIcon()
                {
                    Icon = "Minecraft",
                    IconFontFamily = "FileIcons"
                },
                Category = "Game"
            },
            new ProxyType()
            {
                Name = Languages.Text_CreateProxyGuide_RDPName,
                Description = Languages.Text_CreateProxyGuide_RDPDesc,
                Icon = new PackIcon()
                {
                    Icon = "Airplay",
                    IconFontFamily = "Lucide"
                },
                Category = "Productivity"
            }
        ];
    }
}

public class ProxyType
{
    public string Name
    {
        get;
        set;
    }

    public string Description
    {
        get;
        set;
    }

    public PackIcon Icon
    {
        get;
        set;
    }

    public string Category
    {
        get;
        init;
    }
}

public class PackIcon
{
    public string Icon
    {
        get;
        init;
    }

    public object? IconFontFamily
    {
        get;
        init;
    }
}

public class PackIconToControlConverter : IValueConverter
{
    public static PackIconToControlConverter Instance
    {
        get;
    } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not PackIcon packIcon || string.IsNullOrEmpty(packIcon.Icon))
        {
            return null;
        }

        var iconFontFamily = packIcon.IconFontFamily?.ToString()?.ToLower();

        switch (iconFontFamily)
        {
            case "material":
                if (Enum.TryParse<PackIconMaterialKind>(packIcon.Icon, out var materialKind))
                {
                    return new PackIconMaterial()
                    {
                        Kind = materialKind
                    };
                }

                break;
            case "simpleicons":
                if (Enum.TryParse<PackIconSimpleIconsKind>(packIcon.Icon, true, out var simpleIconsKind))
                {
                    return new PackIconSimpleIcons()
                    {
                        Kind = simpleIconsKind
                    };
                }

                break;
            case "fileicons":
                if (Enum.TryParse<PackIconFileIconsKind>(packIcon.Icon, true, out var fileIconsKind))
                {
                    return new PackIconFileIcons()
                    {
                        Kind = fileIconsKind
                    };
                }
                break;
            case "lucide":
                if (Enum.TryParse<PackIconLucideKind>(packIcon.Icon, true, out var lucideKind))
                {
                    return new PackIconLucide()
                    {
                        Kind = lucideKind
                    };
                }
                break;
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class IconFontFamilyConverter : IValueConverter
{
    public static IconFontFamilyConverter Instance
    {
        get;
    } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string ff && ff.Equals("Material", StringComparison.OrdinalIgnoreCase);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class IconToKindConverter : IValueConverter
{
    public static IconToKindConverter Instance
    {
        get;
    } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string iconName
            ? Enum.TryParse<PackIconSimpleIconsKind>(iconName, out var kind) ? kind : null
            : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}