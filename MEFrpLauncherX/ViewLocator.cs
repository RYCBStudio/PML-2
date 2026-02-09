using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using MEFrpLauncherX.Core;

namespace MEFrpLauncherX;
public class ViewLocator : IDataTemplate
{

    public Control? Build(object? param)
    {
        if (param is null)
        {
            return null;
        }

        try 
        {
            var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
            var type = Type.GetType(name);
        
            return type != null ? (Control)Activator.CreateInstance(type)! : new TextBlock { Text = "View Not Found" };
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Error(ex);
            return new TextBlock { Text = "Error Creating View" };
        }
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
