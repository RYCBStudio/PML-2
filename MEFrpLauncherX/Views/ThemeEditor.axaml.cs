using System;
using Avalonia.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.ViewModels;

namespace MEFrpLauncherX.Views;

public partial class ThemeEditor : Window
{
    public ThemeEditor() : this(null)
    {
    }

    public ThemeEditor(string? themeFilePath)
    {
        InitializeComponent();
        var viewModel = new ThemeEditorViewModel(themeFilePath);
        DataContext = viewModel;
        FontPreviewBox.Text = $"""
                              The quick brown fox jumps over a lazy dog.
                              {CrashHandler.Jokes[Random.Shared.Next(0, CrashHandler.Jokes.Length - 1)]}
                              0123456789
                              ABCDEFGHIJKLMNOPQRSTUVWXYZ
                              """;
    }
}