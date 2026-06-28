using System;
using System.Globalization;
using Avalonia.Collections;
using Avalonia.Data.Converters;
using IconPacks.Avalonia.FileIcons;
using IconPacks.Avalonia.Lucide;
using IconPacks.Avalonia.Material;
using IconPacks.Avalonia.SimpleIcons;
using MEFrpLauncherX.Core;
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
                Description =
                    "AList/OpenList是开源网盘聚合工具，支持多网盘统一管理、文件浏览与视频播放。默认HTTP端口5244，HTTPS端口5245，提供WebDAV服务，可轻松实现本地与云端数据互通。",
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
                Description = "Minecraft Java版多人游戏服务器，支持模组和插件扩展。默认TCP端口25565，可通过SRV记录隐藏端口，适合与朋友联机建造和探索虚拟世界。",
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
                Description = "Minecraft Bedrock版多人游戏服务器，支持模组和插件扩展。默认TCP端口19132，可通过SRV记录隐藏端口，适合与朋友联机建造和探索虚拟世界。",
                Icon = new PackIcon()
                {
                    Icon = "Minecraft",
                    IconFontFamily = "FileIcons"
                },
                Category = "Game"
            },
            new ProxyType()
            {
                Name = "远程桌面连接",
                Description = "RDP（远程桌面协议）是微软开发的远程访问协议，默认使用TCP 3389端口，允许用户通过网络图形化控制远程计算机。",
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
        set;
    }
}

public class PackIcon
{
    public string Icon
    {
        get;
        set;
    }

    public object? IconFontFamily
    {
        get;
        set;
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