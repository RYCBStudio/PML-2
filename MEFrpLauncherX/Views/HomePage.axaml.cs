using System;
using Avalonia.Controls;
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
        AttachedToVisualTree += (s, e) =>
        {
            if (!Design.IsDesignMode)
            {
                DataContext = new HomePageViewModel();
            }
        };
    }

    public void Dispose()
    {
        SuppressFinalize(this);
    }
}