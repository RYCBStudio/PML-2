using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;
using MEFrpLauncherX.ViewModels;

namespace MEFrpLauncherX.Models;

public class PackIconTemplateSelector : IDataTemplate
{
    [Content]
    public Dictionary<string, IDataTemplate> AvailableTemplates
    {
        get;
    } = new Dictionary<string, IDataTemplate>();

    public Control? Build(object? param)
    {
        var key = param?.ToString();
        if (key is null)
        {
            throw new ArgumentNullException(nameof(param));
        }

        return AvailableTemplates[key].Build(param);
    }


    public bool Match(object? data)
    {
        return data is PackIcon packIcon 
               && !string.IsNullOrEmpty(packIcon.IconFontFamily?.ToString())
               && AvailableTemplates.ContainsKey(packIcon.IconFontFamily.ToString()!);
    }
}