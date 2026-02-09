using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FluentAvalonia.UI.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
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
        if (await MessageBox.ShowAsync("打开配置文件还是输入内容？", "提示",
                buttons:
                [
                    new TaskDialogButton("文件", TaskDialogStandardResult.Yes),
                    new TaskDialogButton("内容", TaskDialogStandardResult.No)
                ]) ==
            MessageBoxResult.Yes)
        {
            var cfg = await Core.App.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "请选择配置文件",
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