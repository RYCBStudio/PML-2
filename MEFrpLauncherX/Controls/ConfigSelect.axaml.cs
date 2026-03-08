using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Xml;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace MEFrpLauncherX.Controls;

public partial class ConfigSelect : UserControl, INotifyPropertyChanged
{
    public AvaloniaList<string> Paths
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = [];

    public int Count => Paths.Count;

    public int SelectedIndex
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = 0;

    public string SelectedPath
    {
        get
        {
            if (SelectedIndex < 0 || SelectedIndex >= Count)
                return null;
            var p = Paths[SelectedIndex];
            ConfigPresenter.Load(new FileStream(p, FileMode.Open, FileAccess.Read));
            field = p;
            return p;
        }
        set;
    }

    public ConfigSelect()
    {
        InitializeComponent();
    }

    public ConfigSelect(IEnumerable<string> paths)
    {
        InitializeComponent();
        Paths.AddRange(paths);
        DataContext = this;
        ConfigPresenter.Load(new FileStream(SelectedPath, FileMode.Open, FileAccess.Read));
        SetupSyntaxHighlighting(Path.GetExtension(SelectedPath));
    }

    private void SetupSyntaxHighlighting(string type)
    {
        // Create registry with default themes
        var registryOptions = new RegistryOptions(App.Current?.ActualThemeVariant == ThemeVariant.Dark
            ? ThemeName.DarkPlus
            : ThemeName.LightPlus);

        // Setup TextMate for the editor
        var textMateInstallation = ConfigPresenter.InstallTextMate(registryOptions);
        TextMate.RegisterExceptionHandler(ex =>
        {
            // Handle exceptions from TextMate
            Core.App.CurrentLogger.Log("TextMate Error: " + ex.Message);
            Core.App.CurrentLogger.Error(ex);
        });
        // Get the grammar based on file type
        var language = registryOptions.GetLanguageByExtension(type);
        if (type == ".toml")
        {
            var resourceName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory + "Resources", "toml.xshd");
            using Stream s = new FileStream(resourceName, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using XmlTextReader reader = new(s);

            var xshd = HighlightingLoader.LoadXshd(reader);
            ConfigPresenter.SyntaxHighlighting = HighlightingLoader.Load(xshd, HighlightingManager.Instance);
            return;
        }

        if (language != null)
        {
            var scope = registryOptions.GetScopeByLanguageId(language.Id);
            if (scope != null)
            {
                textMateInstallation.SetGrammar(scope);
            }
            else
            {
                // Handle missing scope case
                Core.App.CurrentLogger.Log($"No scope found for language ID: {language.Id}");
            }
        }
        else
        {
            // Handle missing language case
            Core.App.CurrentLogger.Log($"No language found for extension: {type}");
        }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void PrevFile(object? sender, RoutedEventArgs e)
    {
        SelectedIndex--;
        if (SelectedIndex < 0)
            SelectedIndex = 0;
        var p = Paths[SelectedIndex];
        SelectedPath = p;
        ConfigPath.Text = p;
        ConfigPresenter.Load(new FileStream(p, FileMode.Open, FileAccess.Read));
    }

    private void NextFile(object? sender, RoutedEventArgs e)
    {
        SelectedIndex++;
        if (SelectedIndex >= Count)
            SelectedIndex = Count - 1;
        var p = Paths[SelectedIndex];
        SelectedPath = p;
        ConfigPath.Text = p;
        ConfigPresenter.Load(new FileStream(p, FileMode.Open, FileAccess.Read));
    }
}