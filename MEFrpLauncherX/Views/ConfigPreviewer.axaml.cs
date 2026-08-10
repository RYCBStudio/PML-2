using System;
using System.IO;
using System.Web;
using System.Xml;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using AvaloniaEdit.TextMate;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.Languages;
using TextMateSharp.Grammars;

#pragma warning disable CS8622 // 参数类型中引用类型的为 Null 性与目标委托不匹配(可能是由于为 Null 性特性)。

namespace MEFrpLauncherX.Views;

public partial class ConfigPreviewer : Window
{
    internal static readonly FilePickerFileType fpftype_yml = new(Languages.Text_ConfigPreviewer_YamlFile) { Patterns = ["*.yaml", "*.yml"] };
    internal static readonly FilePickerFileType fpftype_ini = new(Languages.Text_ConfigPreviewer_IniFile) { Patterns = ["*.ini"] };
    internal static readonly FilePickerFileType fpftype_toml = new(Languages.Text_ConfigPreviewer_TomlFile) { Patterns = ["*.toml"] };
    internal static readonly FilePickerFileType fpftype_json = new(Languages.Text_ConfigPreviewer_JsonFile) { Patterns = ["*.json"] };
    private readonly string _config;
    private readonly string _proxyname;
    private readonly string _type;

    public ConfigPreviewer(string type, string config, string proxyName)
    {
        InitializeComponent();
        _type = type;
        _config = config;
        _proxyname = proxyName;
        Loaded += ConfigPreviewer_Loaded;
        ConfigTypeTextBlock.Text = type.ToUpper();
    }

    public ConfigPreviewer(string type, string config)
    {
        InitializeComponent();
        _type = type;
        _config = config;
        Loaded += ConfigPreviewer_Loaded;
        ConfigTypeTextBlock.Text = type.ToUpper();
    }

    public ConfigPreviewer(string file)
    {
        InitializeComponent();
        _type = Path.GetExtension(file).Remove(0, 1);
        ;
        _config = file;
        Loaded += ConfigPreviewer_Loaded;
        ConfigTypeTextBlock.Text = _type.ToUpper();
    }

    private async void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        await Clipboard.SetTextAsync(ConfigEditor.Text);
        // Replace Growl with Avalonia notification system
        ShowNotification(Languages.Text_ConfigPreviewer_CopiedToClipboard);
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Languages.Text_ConfigPreviewer_SaveConfigTitle,
            SuggestedFileName = $"{(_proxyname.IsNullOrEmpty() ? "config" : _proxyname)}.{_type.ToLower()}",
            SuggestedStartLocation =
                await StorageProvider.TryGetFolderFromPathAsync(new Uri($"file:///{Core.App.StartupPath}/Config/frp")),
            FileTypeChoices =
            [
                fpftype_toml,
                fpftype_yml,
                fpftype_json,
                fpftype_ini
            ],
            SuggestedFileType = _type.ToLower() switch
            {
                "toml" => fpftype_toml,
                "yml" or "yaml" => fpftype_yml,
                "json" => fpftype_json,
                "ini" => fpftype_ini,
                _ => fpftype_yml
            }
        });

        if (file == null)
        {
            return;
        }

        await File.WriteAllTextAsync(HttpUtility.UrlDecode(file.Path.AbsolutePath), ConfigEditor.Text);
        ShowNotification(string.Format(Languages.Text_ConfigPreviewer_SavedFormat, file.Name));
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void ConfigPreviewer_Loaded(object sender, RoutedEventArgs e)
    {
        // Apply theme colors
        // if (ConfigManager.CurrentConfig.Skin == "Dark")
        // {
        //     ConfigEditor.Background = (IBrush)Application.Current.FindResource("DarkBackGroundBrush");
        //     ConfigEditor.Foreground = (IBrush)Application.Current.FindResource("LightBackGroundBrush");
        // }
        // else
        // {
        //     ConfigEditor.Foreground = (IBrush)Application.Current.FindResource("DarkBackGroundBrush");
        //     ConfigEditor.Background = (IBrush)Application.Current.FindResource("LightBackGroundBrush");
        // }

        // Setup syntax highlighting using TextMate
        SetupSyntaxHighlighting();

        ConfigEditor.Text = _config;
    }

    private void SetupSyntaxHighlighting()
    {
        // Create registry with default themes
        var registryOptions = new RegistryOptions(ThemeName.DarkPlus);

        // Setup TextMate for the editor
        var textMateInstallation = ConfigEditor.InstallTextMate(registryOptions);
        // TextMate.RegisterExceptionHandler(ex =>
        // {
        //     // Handle exceptions from TextMate
        //     Core.App.CurrentLogger.Log("TextMate Error: " + ex.Message);
        //     Core.App.CurrentLogger.Error(ex);
        // });
        // Get the grammar based on file type
        var scopeName = _type.ToLower() switch
        {
            "yaml" or "yml" => ".yaml",
            "ini" => ".ini",
            "toml" => ".toml",
            "json" => ".json",
            _ => ".txt"
        };
        var language = registryOptions.GetLanguageByExtension(scopeName);
        if (scopeName == ".toml")
        {
            var resourceName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory + "Resources", "toml.xshd");
            using Stream s = new FileStream(resourceName, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using XmlTextReader reader = new(s);

            var xshd = HighlightingLoader.LoadXshd(reader);
            ConfigEditor.SyntaxHighlighting = HighlightingLoader.Load(xshd, HighlightingManager.Instance);
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
            Core.App.CurrentLogger.Log($"No language found for extension: {scopeName}");
        }
    }

    private static void ShowNotification(string message) => Growl.Info(message);
}