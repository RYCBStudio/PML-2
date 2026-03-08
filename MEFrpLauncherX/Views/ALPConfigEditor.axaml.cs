using System;
using System.IO;
using System.Xml;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using AvaloniaEdit.TextMate;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MsBox.Avalonia.Enums;
using TextMateSharp.Grammars;
using static MEFrpLauncherX.Views.ConfigPreviewer;

namespace MEFrpLauncherX.Views;

public partial class ALPConfigEditor : Window
{
    private string _path;
    private string _type;

    public string Path
    {
        get => _path;
        set => _path = value;
    }

    public ALPConfigEditor()
    {
        InitializeComponent();
    }

    private async void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        if (File.Exists(_path))
        {
            using (var s = new FileStream(_path, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                ConfigEdit.Save(s);
            }
            UnSavedTip.Hide();
        }
        else
        {
            var cfg = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "请选择保存配置文件的路径",
                SuggestedFileType = _type.ToLower() switch
                {
                    "toml" => fpftype_toml,
                    "yml" or "yaml" => fpftype_yml,
                    "json" => fpftype_json,
                    "ini" => fpftype_ini,
                    _ => fpftype_yml
                },
                FileTypeChoices =
                [
                    fpftype_toml, fpftype_json, fpftype_yml,
                    fpftype_ini
                ],
            });
            var configFile = string.Empty;
            try
            {
                configFile = cfg?.Path.AbsolutePath;
            }
            catch (ArgumentOutOfRangeException)
            {
            }
            catch (Exception ex)
            {
                Core.App.CurrentLogger.Error(ex);
            }

            _path = configFile;
            if (File.Exists(_path))
            {
                using (var s = new FileStream(_path, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    ConfigEdit.Save(s);
                }
            }

            UnSavedTip.Hide();
        }
    }

    private async void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        if (ConfigEdit.IsModified)
        {
            if (await MessageBox.ShowAsync("配置文件已修改，是否返回并保存？", "提示", ButtonEnum.YesNo) == MessageBoxResult.Yes)
            {
                OnSaveClicked(sender, e);
                UnSavedTip.Hide();
            }
            else
            {
                Close();
            }
        }
        else
        {
            Close();
        }
    }

    private async void OnPasteClicked(object? sender, RoutedEventArgs e)
    {
        var content = await Clipboard?.TryGetTextAsync();
        if (content != null)
        {
            ConfigEdit.Text = content;
            var type = FileFormatDetector.DetectFormatFromContent(content);
            _type = type.ToLower();
            SetupSyntaxHighlighting();
        }
        else
        {
            await MessageBox.ShowAsync("粘贴失败，请检查剪贴板内容。", "错误", MessageBoxIcon.Warning);
        }
    }

    private void ChangeSyntaxHighlight(object? sender, SelectionChangedEventArgs e)
    {
        _type = ((ComboBoxItem)(((ComboBox)sender)?.SelectedItem))?.Content.ToString();
        SetupSyntaxHighlighting();
    }

    private void SetupSyntaxHighlighting()
    {
        // Create registry with default themes
        var registryOptions = new RegistryOptions(ThemeName.DarkPlus);

        // Setup TextMate for the editor
        var textMateInstallation = ConfigEdit.InstallTextMate(registryOptions);
        TextMate.RegisterExceptionHandler(ex =>
        {
            // Handle exceptions from TextMate
            Core.App.CurrentLogger.Log("TextMate Error: " + ex.Message);
            Core.App.CurrentLogger.Error(ex);
        });
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
            var resourceName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory + "Resources", "toml.xshd");
            using Stream s = new FileStream(resourceName, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using XmlTextReader reader = new(s);

            var xshd = HighlightingLoader.LoadXshd(reader);
            ConfigEdit.SyntaxHighlighting = HighlightingLoader.Load(xshd, HighlightingManager.Instance);
            ConfigEdit.InvalidateVisual();
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

    private async void Window_OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (!ConfigEdit.IsModified || await MessageBox.ShowAsync("配置文件已修改，是否返回并保存？", "提示", ButtonEnum.YesNo) != MessageBoxResult.Yes)
        {
            e.Cancel = false;
        }
        else
        {
            e.Cancel = true;
            OnSaveClicked(sender, null);
            UnSavedTip.Hide();
        }
    }
}