using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Core.ViewModels;
using MEFrpLauncherX.Services;
using MEFrpLauncherX.Views;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia.ViewModels.Commands;
using ReactiveUI;
using SecretLib;

namespace MEFrpLauncherX.ViewModels;

public class ThemesPageViewModel : ViewModelBase
{
    private readonly ThemeService _themeService = new();

    public ObservableCollection<LocalTheme> LocalThemes
    {
        get;
    } = [];

    public ObservableCollection<OnlineTheme> OnlineThemes
    {
        get;
    } = [];

    public AvaloniaList<OnlineTheme> FilteredOnlineThemes
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    public LocalTheme? SelectedLocalTheme
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public OnlineTheme? SelectedOnlineTheme
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsLoadingOnlineThemes
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsDownloading
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ReactiveCommand<Unit, Unit> RefreshLocalThemesCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> FetchOnlineThemesCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> DownloadThemeCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> ApplyLocalThemeCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> EditLocalThemeCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> DeleteLocalThemeCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> PackageLocalThemeCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> AddLocalThemeCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> ImportThemeCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> OpenDocumentationCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> RestoreDefaultThemeCommand
    {
        get;
    }

    public string? SearchText
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ThemesPageViewModel()
    {
        RefreshLocalThemesCommand = ReactiveCommand.Create(RefreshLocalThemes);
        FetchOnlineThemesCommand = ReactiveCommand.CreateFromTask(FetchOnlineThemesAsync);
        DownloadThemeCommand = ReactiveCommand.CreateFromTask(DownloadThemeAsync,
            this.WhenAnyValue(x => x.SelectedOnlineTheme, (OnlineTheme? theme) => theme != null));
        ApplyLocalThemeCommand = ReactiveCommand.Create(ApplyLocalTheme,
            this.WhenAnyValue(x => x.SelectedLocalTheme, (LocalTheme? theme) => theme != null));
        DeleteLocalThemeCommand = ReactiveCommand.CreateFromTask(DeleteLocalThemeAsync,
            this.WhenAnyValue(x => x.SelectedLocalTheme, (LocalTheme? theme) => theme != null));
        EditLocalThemeCommand = ReactiveCommand.Create(EditLocalTheme,
            this.WhenAnyValue(x => x.SelectedLocalTheme, (LocalTheme? theme) => theme != null));
        PackageLocalThemeCommand = ReactiveCommand.Create(PackageLocalTheme,
            this.WhenAnyValue(x => x.SelectedLocalTheme, (LocalTheme? theme) => theme != null));
        AddLocalThemeCommand = ReactiveCommand.Create(AddLocalTheme);
        ImportThemeCommand = ReactiveCommand.Create(ImportTheme);
        OpenDocumentationCommand = ReactiveCommand.Create(() =>
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://docs.rycb.tech/pml-2/themes",
                UseShellExecute = true
            });
        });
        RestoreDefaultThemeCommand = ReactiveCommand.CreateFromTask(RestoreDefaultThemeAsync);
        FetchOnlineThemesCommand.Execute();
        this.WhenAnyValue(x => x.SearchText).Throttle(TimeSpan.FromMilliseconds(300)).Subscribe(text =>
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                FilteredOnlineThemes.Clear();
                FilteredOnlineThemes.AddRange(OnlineThemes);
            }
            else
            {
                FilteredOnlineThemes.Clear();
                var query = from theme in OnlineThemes
                    where theme.Name.Contains(text) || theme.Author.Contains(text) || theme.Description.Contains(text)
                    select theme;
                FilteredOnlineThemes.AddRange(query);
            }
        });
        RefreshLocalThemes();
    }

    private async void ImportTheme()
    {
        var res = await MainWindow.Instance.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType(Languages.Text_Themes_ThemeFiles)
                {
                    Patterns = ["*.pmla"]
                }
            ],
            SuggestedFileType = new FilePickerFileType(Languages.Text_Themes_ThemeFiles)
            {
                Patterns = ["*.pmla"]
            },
            Title = Languages.Text_Themes_ImportThemeTitle,
            SuggestedStartLocation =
                await MainWindow.Instance.StorageProvider.TryGetFolderFromPathAsync(Path.Combine(Core.App.StartupPath,
                    "Config", "Themes"))
        });
        if (res.Count <= 0)
        {
            return;
        }

        var cnt = 0;
        var total = res.Count;
        for (var i = 0; i < total; i++)
        {
            var storageFile = res[i];
            MainWindowViewModel.Instance.AppMessage = string.Format(Languages.Text_Themes_ImportingThemeFormat, storageFile.Name);
            MainWindowViewModel.Instance.Progress = cnt;
            PMLAHelper.UnpackPmla(storageFile.TryGetLocalPath(),
                Path.Combine(Core.App.StartupPath, "Config", "Themes",
                    Path.GetFileNameWithoutExtension(storageFile.Name)));
            if (i == total - 1)
            {
                cnt = 100;
            }
            else
            {
                cnt += 100 / total;
            }
        }
    }

    private void AddLocalTheme()
    {
        var editor = new ThemeEditor();
        try
        {
            editor.ShowDialog(Core.App.MainWindow);
        }
        catch
        {
            editor.Show();
        }
    }

    private void EditLocalTheme()
    {
        if (SelectedLocalTheme == null) return;

        var editor = new ThemeEditor(SelectedLocalTheme.Path);
        try
        {
            editor.ShowDialog(Core.App.MainWindow);
        }
        catch
        {
            editor.Show();
        }
    }

    private void RefreshLocalThemes()
    {
        LocalThemes.Clear();
        foreach (var theme in ThemeService.GetLocalThemes())
        {
            LocalThemes.Add(theme);
        }

        SelectedLocalTheme = LocalThemes.Select(t => t.Name).Contains(Core.App.SelectedTheme)
            ? LocalThemes.First(t => t.Name == Core.App.SelectedTheme)
            : null;
    }

    private async Task FetchOnlineThemesAsync()
    {
        IsLoadingOnlineThemes = true;
        try
        {
            OnlineThemes.Clear();
            var themes = await _themeService.FetchOnlineThemesAsync();
            foreach (var theme in themes)
            {
                OnlineThemes.Add(theme);
            }
            FilteredOnlineThemes.Clear();
            FilteredOnlineThemes.AddRange(OnlineThemes);
        }
        catch (Exception ex)
        {
            await MessageBoxManager.GetMessageBoxStandard(Languages.Caption_Error, string.Format(Languages.Text_Themes_FetchOnlineFailedFormat, ex.Message), ButtonEnum.Ok, Icon.Error)
                .ShowAsync();
        }
        finally
        {
            IsLoadingOnlineThemes = false;
        }
    }

    private async Task DownloadThemeAsync()
    {
        if (SelectedOnlineTheme == null) return;

        IsDownloading = true;
        try
        {
            var themesDir = Path.Combine(Core.App.StartupPath, "Config", "Themes");
            if (!Directory.Exists(themesDir))
            {
                Directory.CreateDirectory(themesDir);
            }
            var downloadPath = Path.Combine(themesDir, $"{SelectedOnlineTheme.Name}.pmla");

            var success = await _themeService.DownloadThemeAsync(SelectedOnlineTheme, downloadPath);
            if (success)
            {
                await MessageBoxManager.GetMessageBoxStandard(Languages.Caption_Success, Languages.Text_Themes_ThemeDownloaded, ButtonEnum.Ok, Icon.Info).ShowAsync();
                RefreshLocalThemes();
            }
            else
            {
                await MessageBoxManager.GetMessageBoxStandard(Languages.Caption_Error, Languages.Text_Themes_ThemeDownloadFailed, ButtonEnum.Ok, Icon.Error).ShowAsync();
            }
        }
        catch (Exception ex)
        {
            await MessageBoxManager.GetMessageBoxStandard(Languages.Caption_Error, string.Format(Languages.Text_Themes_DownloadFailedFormat, ex.Message), ButtonEnum.Ok, Icon.Error)
                .ShowAsync();
        }
        finally
        {
            IsDownloading = false;
        }
    }

    private void ApplyLocalTheme()
    {
        if (SelectedLocalTheme == null) return;

        _themeService.ApplyTheme(SelectedLocalTheme.Path);
        Growl.Success(Languages.Text_Themes_ThemeApplied);
    }

    private async Task DeleteLocalThemeAsync()
    {
        if (SelectedLocalTheme == null) return;

        var result = await MessageBoxManager
            .GetMessageBoxStandard(Languages.Text_Global_Confirm, string.Format(Languages.Text_Themes_DeleteConfirmFormat, SelectedLocalTheme.Name), ButtonEnum.YesNo, Icon.Question)
            .ShowAsync();
        if (result == ButtonResult.Yes)
        {
            _themeService.DeleteTheme(SelectedLocalTheme.Path);
            RefreshLocalThemes();
            Growl.Success(Languages.Text_Themes_ThemeDeleted);
        }
    }

    private async void PackageLocalTheme()
    {
        if (SelectedLocalTheme == null) return;
        var btn = new TaskDialogButton
        {
            DialogResult = TaskDialogStandardResult.Cancel,
            Text = Languages.Text_Global_Cancel,
            Command = new RelayCommand(async _ =>
            {
            })
        };
        var path = Path.Combine(Core.App.StartupPath, "Config", "Themes Output");
        var td = new TaskDialog
        {
            Title = Languages.Text_Themes_PackagingTheme,
            ShowProgressBar = true,
            IconSource = new SymbolIconSource { Symbol = Symbol.Download },
            SubHeader = string.Format(Languages.Text_Themes_ThemeNameFormat, SelectedLocalTheme.Name),
            Content = string.Format(Languages.Text_Themes_PackageInfoFormat, SelectedLocalTheme.Version,
                SelectedLocalTheme.Author, path),
            Buttons =
            {
                btn
            }
        };
        td.SetProgressBarState(0, TaskDialogProgressState.Indeterminate);
        td.XamlRoot = TopLevel.GetTopLevel(Core.App.MainWindow);
        td.ShowAsync();

        Directory.CreateDirectory(path);
        await Task.Run(() => PMLAHelper.PackDirectory(SelectedLocalTheme.Path,
            Path.Combine(path, $"{SelectedLocalTheme.Name}.pmla"),
            (progress, status) =>
            {
                td.SetProgressBarState(progress, TaskDialogProgressState.Normal);
            }));
        Dispatcher.UIThread.Post(() =>
        {
            td.Hide(TaskDialogStandardResult.OK);
        });
        Growl.Success(Languages.Text_Themes_ThemePackaged);
        await MessageBox.ShowAsync(Languages.Text_Themes_ThemePackageCompleted, buttons:
        [
            new TaskDialogButton
            {
                Text = Languages.Text_Themes_OpenFolder,
                Command = new RelayCommand(_ =>
                {
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                })
            },
            new TaskDialogButton
            {
                Text = Languages.Text_Global_Confirm,
                Command = new RelayCommand(_ =>
                {
                })
            }
        ]);
    }

    private async Task RestoreDefaultThemeAsync()
    {
        var result = await MessageBoxManager
            .GetMessageBoxStandard(Languages.Text_Themes_RestoreDefaultCaption,
                Languages.Text_Themes_RestoreDefaultConfirm,
                ButtonEnum.YesNo, Icon.Question)
            .ShowAsync();

        if (result != ButtonResult.Yes) return;

        var selectedPath = Path.Combine(Core.App.StartupPath, "Config", "Themes", "selected");
        try
        {
            if (File.Exists(selectedPath))
            {
                File.Delete(selectedPath);
            }

            Core.App.SelectedTheme = null;
            RefreshLocalThemes();

            await MessageBoxManager
                .GetMessageBoxStandard(Languages.Caption_Hint,
                    Languages.Text_Themes_DefaultThemeRestored,
                    ButtonEnum.Ok, Icon.Info)
                .ShowAsync();
        }
        catch (Exception ex)
        {
            await MessageBoxManager
                .GetMessageBoxStandard(Languages.Caption_Error, string.Format(Languages.Text_Themes_RestoreDefaultFailedFormat, ex.Message), ButtonEnum.Ok, Icon.Error)
                .ShowAsync();
        }
    }
}