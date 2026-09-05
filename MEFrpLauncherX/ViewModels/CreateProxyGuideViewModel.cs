using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using IconPacks.Avalonia.FileIcons;
using IconPacks.Avalonia.Lucide;
using IconPacks.Avalonia.Material;
using IconPacks.Avalonia.SimpleIcons;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Plugin.Core;
using MEFrpLauncherX.Plugin.Services;
using ReactiveUI;

namespace MEFrpLauncherX.ViewModels;

public class CreateProxyGuideViewModel : ViewModelBase
{
    /// <summary>
    ///     引导模板条目列表，数据源为 create-proxy-template 类型插件的 templates 声明
    ///     （官方内置 + 第三方合并），不再硬编码。
    /// </summary>
    public AvaloniaList<ProxyType> AvailableTypes
    {
        get;
        set;
    } = new();

    public ProxyType? SelectedType
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>是否有可用的引导模板（无模板插件时用于空态提示）</summary>
    public bool HasTemplates => AvailableTypes.Count > 0;

    public CreateProxyGuideViewModel()
    {
        Refresh();
    }

    /// <summary>
    ///     重新拉取启用的模板插件条目（Tab 重进 / 插件启停 / 热重载后调用）。
    ///     设计时（Designer）不访问插件目录，保持空列表。
    /// </summary>
    public void Refresh()
    {
        if (Design.IsDesignMode)
        {
            return;
        }

        var list = new AvaloniaList<ProxyType>();
        foreach (var entry in PluginService.Instance.GetEnabledProxyTemplateEntries())
        {
            var t = entry.Definition;
            if (string.IsNullOrWhiteSpace(t.Name) || t.Id.IsNullOrEmpty())
            {
                continue;
            }

            list.Add(new ProxyType
            {
                TemplateId = t.Id,
                PluginId = entry.PluginId,
                SourceTemplate = t,
                Name = ResolveLocalized(t.NameLocalized, t.Name),
                Description = ResolveLocalized(t.DescriptionLocalized, t.Description),
                Icon = new PackIcon
                {
                    Icon = t.Icon.Name,
                    IconFontFamily = t.Icon.Pack
                },
                Category = t.Category
            });
        }

        AvailableTypes = list;
        this.RaisePropertyChanged(nameof(HasTemplates));
    }

    /// <summary>
    ///     按当前 UI 语言从本地化字典取值（key: zh-hans/zh-hant/en 等），缺省回退 fallback。
    /// </summary>
    private static string ResolveLocalized(Dictionary<string, string>? localized, string fallback)
    {
        if (localized is null || localized.Count == 0)
        {
            return fallback;
        }

        var culture = CultureInfo.CurrentUICulture;
        if (localized.TryGetValue(culture.Name, out var exact))
        {
            return exact;
        }

        if (localized.TryGetValue(culture.Name.ToLowerInvariant(), out var lower))
        {
            return lower;
        }

        // 中文简繁归一：zh-TW/zh-HK/zh-MO/zh-Hant → zh-hant，其余 zh 开头视为 zh-hans
        if (culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            var key = culture.Name.Equals("zh-hant", StringComparison.OrdinalIgnoreCase) ||
                      culture.Name.Equals("zh-tw", StringComparison.OrdinalIgnoreCase) ||
                      culture.Name.Equals("zh-hk", StringComparison.OrdinalIgnoreCase) ||
                      culture.Name.Equals("zh-mo", StringComparison.OrdinalIgnoreCase)
                ? "zh-hant"
                : "zh-hans";
            if (localized.TryGetValue(key, out var zh))
            {
                return zh;
            }
        }

        if (localized.TryGetValue(culture.TwoLetterISOLanguageName, out var twoLetter))
        {
            return twoLetter;
        }

        return fallback;
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

    /// <summary>来源模板声明 id（同一插件内唯一）</summary>
    public string TemplateId
    {
        get;
        init;
    } = "";

    /// <summary>来源插件 id（多个 create-proxy-template 插件合并时用于追踪）</summary>
    public string PluginId
    {
        get;
        init;
    } = "";

    /// <summary>模板定义对象（引导流程据此取创建默认值/节点筛选/副隧道声明）</summary>
    public ProxyTemplateDefinition? SourceTemplate
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