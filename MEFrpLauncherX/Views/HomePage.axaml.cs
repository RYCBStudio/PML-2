using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.MEFIntergrated;
using MEFrpLauncherX.ViewModels;
using ReactiveUI.Avalonia;
using static System.GC;

#pragma warning disable CS8602 // 解引用可能出现空引用。

namespace MEFrpLauncherX.Views;

public partial class HomePage : ReactiveUserControl<HomePageViewModel>, IDisposable
{
    public HomePage()
    {
        InitializeComponent();
        DataContext = null;
        AttachedToVisualTree += async (s, e) =>
        {
            var vm = new HomePageViewModel();
            if (!Design.IsDesignMode)
            {
                DataContext = vm;
            }

            await Task.Delay(4000);
            if (ConfigManager.CurrentConfig.AutoSign && vm.CanSign)
            {
                vm.SignCommand.Execute().Subscribe();
            }
        };
    }

    public void Dispose() => SuppressFinalize(this);
}