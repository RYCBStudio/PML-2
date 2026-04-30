using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Services;
using MEFrpLauncherX.Core.Controls;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using ReactiveUI;

namespace MEFrpLauncherX.ViewModels;

public class ThemesPageViewModel : ViewModelBase
{
    private readonly ThemeService _themeService = new();

    public ObservableCollection<LocalTheme> LocalThemes { get; } = new();
    public ObservableCollection<OnlineTheme> OnlineThemes { get; } = new();

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

    public ReactiveCommand<Unit, Unit> RefreshLocalThemesCommand { get; }
    public ReactiveCommand<Unit, Unit> FetchOnlineThemesCommand { get; }
    public ReactiveCommand<Unit, Unit> DownloadThemeCommand { get; }
    public ReactiveCommand<Unit, Unit> ApplyLocalThemeCommand { get; }
    public ReactiveCommand<Unit, Unit> EditLocalThemeCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteLocalThemeCommand { get; }

    public ThemesPageViewModel()
    {
        RefreshLocalThemesCommand = ReactiveCommand.Create(RefreshLocalThemes);
        FetchOnlineThemesCommand = ReactiveCommand.CreateFromTask(FetchOnlineThemesAsync);
        DownloadThemeCommand = ReactiveCommand.CreateFromTask(DownloadThemeAsync, this.WhenAnyValue(x => x.SelectedOnlineTheme, (OnlineTheme? theme) => theme != null));
        ApplyLocalThemeCommand = ReactiveCommand.Create(ApplyLocalTheme, this.WhenAnyValue(x => x.SelectedLocalTheme, (LocalTheme? theme) => theme != null));
        DeleteLocalThemeCommand = ReactiveCommand.CreateFromTask(DeleteLocalThemeAsync, this.WhenAnyValue(x => x.SelectedLocalTheme, (LocalTheme? theme) => theme != null));
        EditLocalThemeCommand = ReactiveCommand.Create(EditLocalTheme, this.WhenAnyValue(x => x.SelectedLocalTheme, (LocalTheme? theme) => theme != null));
        
        RefreshLocalThemes();
    }
    
    private void EditLocalTheme()
    {
        if (SelectedLocalTheme == null) return;

        var editor = new Views.ThemeEditor(SelectedLocalTheme.Path);
        editor.ShowDialog(Core.App.MainWindow);
    }

    private void RefreshLocalThemes()
    {
        LocalThemes.Clear();
        foreach (var theme in ThemeService.GetLocalThemes())
        {
            LocalThemes.Add(theme);
        }
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
        }
        catch (Exception ex)
        {
            await MessageBoxManager.GetMessageBoxStandard("错误", $"获取在线主题失败: {ex.Message}", ButtonEnum.Ok, Icon.Error).ShowAsync();
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
            var downloadPath = Path.Combine(themesDir, $"{SelectedOnlineTheme.Name}.zip");

            var success = await _themeService.DownloadThemeAsync(SelectedOnlineTheme, downloadPath);
            if (success)
            {
                await MessageBoxManager.GetMessageBoxStandard("成功", "主题下载完成", ButtonEnum.Ok, Icon.Info).ShowAsync();
                RefreshLocalThemes();
            }
            else
            {
                await MessageBoxManager.GetMessageBoxStandard("错误", "主题下载失败", ButtonEnum.Ok, Icon.Error).ShowAsync();
            }
        }
        catch (Exception ex)
        {
            await MessageBoxManager.GetMessageBoxStandard("错误", $"下载失败: {ex.Message}", ButtonEnum.Ok, Icon.Error).ShowAsync();
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
        Growl.Success("主题已应用");
    }

    private async Task DeleteLocalThemeAsync()
    {
        if (SelectedLocalTheme == null) return;

        var result = await MessageBoxManager.GetMessageBoxStandard("确认", $"确定要删除主题 '{SelectedLocalTheme.Name}' 吗？", ButtonEnum.YesNo, Icon.Question).ShowAsync();
        if (result == ButtonResult.Yes)
        {
            _themeService.DeleteTheme(SelectedLocalTheme.Path);
            RefreshLocalThemes();
            Growl.Success("主题已删除");
        }
    }
}
