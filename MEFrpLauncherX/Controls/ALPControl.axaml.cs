using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FluentAvalonia.UI.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.ViewModels;
using MEFrpLauncherX.Views;

namespace MEFrpLauncherX.Controls;

public partial class ALPControl : UserControl
{
    public ALPControl()
    {
        InitializeComponent();
    }

    private async void SetConfig(object? sender, RoutedEventArgs e)
    {
        if (await MessageBox.ShowAsync(Languages.Text_ALPControl_ConfigChoicePrompt, Languages.Caption_Hint,
            [
                new TaskDialogButton(Languages.Text_ALPControl_FileButton, TaskDialogStandardResult.Yes),
                new TaskDialogButton(Languages.Text_ALPControl_ContentButton, TaskDialogStandardResult.No)
            ]) ==
            MessageBoxResult.Yes)
        {
            var cfg = await Core.App.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Languages.Text_ALPControl_SelectConfigTitle,
                AllowMultiple = false,
                FileTypeFilter = [FilePickerFileTypes.All]
            });
            var configFile = string.Empty;
            if (cfg is not null)
            {
                configFile = cfg?[0].Path.AbsolutePath;
            }

            if (configFile.IsNullOrEmpty())
            {
                return;
            }

            await new ConfigPreviewer(configFile).ShowDialog<bool>(Core.App.MainWindow);
            (DataContext as UserProxyViewModel)?.UseConfig = true;
            (DataContext as UserProxyViewModel)?.Config = configFile;
            SCBtn.IsEnabled = false;
            USCBtn.IsEnabled = true;
        }
        else
        {
            var ce = new ALPConfigEditor();
            await ce.ShowDialog(Core.App.MainWindow);
            (DataContext as UserProxyViewModel)?.UseConfig = true;
            (DataContext as UserProxyViewModel)?.Config = ce.Path;
            SCBtn.IsEnabled = false;
            USCBtn.IsEnabled = true;
        }
    }

    private void UnSetConfig(object? sender, RoutedEventArgs e)
    {
        (DataContext as UserProxyViewModel)?.UseConfig = false;
        (DataContext as UserProxyViewModel)?.Config = string.Empty;
        SCBtn.IsEnabled = true;
        USCBtn.IsEnabled = false;
    }
}