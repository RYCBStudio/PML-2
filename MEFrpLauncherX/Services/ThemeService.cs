using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Media.Imaging;
using MEFrpLauncherX.Core.Styling;
using MEFrpLauncherX.Views;
using ReactiveUI;
using SecretLib;

namespace MEFrpLauncherX.Core.Services;

public class ThemeService
{
    private readonly HttpClient _httpClient = new();

    public async Task<AvaloniaList<OnlineTheme>> FetchOnlineThemesAsync()
    {
        try
        {
            // 假设在线主题列表从 GitHub 获取
            var response =
                await _httpClient.GetStringAsync(
                    "https://alist.yealqp.cn/download/ME-Frp%20PML2/mefrp/market/themes/manifest.json");
            var themes =
                JsonSerializer.Deserialize<List<OnlineTheme>>(response,
                    MEFrpLauncherX.App.AppJsonSerializerContext.ListOnlineTheme);
            return new AvaloniaList<OnlineTheme>(themes ?? []);
        }
        catch
        {
            // 返回空列表，如果失败
            return [];
        }
    }

    public static AvaloniaList<LocalTheme> GetLocalThemes()
    {
        var themesDir = Path.Combine(App.StartupPath, "Config", "Themes");
        var localThemes = new AvaloniaList<LocalTheme>();

        if (Directory.Exists(themesDir))
        {
            foreach (var dir in Directory.GetDirectories(themesDir))
            {
                var indexPath = Path.Combine(dir, "index.json");
                if (File.Exists(indexPath))
                {
                    try
                    {
                        var manifest = ThemeProcessor.LoadTheme(indexPath);
                        if (manifest != null)
                        {
                            localThemes.Add(new LocalTheme
                            {
                                Name = manifest.Name,
                                Author = manifest.Author,
                                Description = manifest.Description,
                                Version = manifest.Version,
                                Path = dir,
                                PreviewImage = new Bitmap(Path.Combine(dir, manifest.PreviewImage ?? "preview.png"))
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        App.CurrentLogger.Warning($"加载主题失败: {dir}, 原因: {ex.Message}");
                        App.CurrentLogger.Error(ex, "加载主题配置文件时发生错误");
                    }
                }
            }
        }

        return localThemes;
    }

    public async Task<bool> DownloadThemeAsync(OnlineTheme theme, string downloadPath)
    {
        try
        {
            using var response = await _httpClient.GetAsync(theme.DownloadUrl);
            response.EnsureSuccessStatusCode();
            await using var fs = new FileStream(downloadPath, FileMode.Create);
            await response.Content.CopyToAsync(fs);

            // 解压主题包
            var themesDir = Path.Combine(App.StartupPath, "Config", "Themes");
            var themeDir = Path.Combine(themesDir, theme.Name);
            if (Directory.Exists(themeDir))
            {
                Directory.Delete(themeDir, true);
            }

            try
            {
                await fs.DisposeAsync();
            }
            catch
            {
            }

            Directory.CreateDirectory(themeDir);
            PMLAHelper.UnpackPmla(downloadPath, themeDir, (progress, status) =>
            {
                MainWindowViewModel.Instance.AppMessage = $"正在解压主题：{progress}%";
                MainWindowViewModel.Instance.Progress = progress;
            });
            File.Delete(downloadPath); // 删除临时 zip 文件

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async void ApplyTheme(string themePath)
    {
        var selectedPath = Path.Combine(App.StartupPath, "Config", "Themes", "selected");
        var themeName = Path.GetFileName(themePath);
        await File.WriteAllTextAsync(selectedPath, themeName);
        App.SelectedTheme = themeName;
        await MainWindow.Instance.ApplyThemeAsync();
    }

    public void DeleteTheme(string themePath)
    {
        if (Directory.Exists(themePath))
        {
            Directory.Delete(themePath, true);
        }
    }
}

public class OnlineTheme
{
    public string Name
    {
        get;
        set;
    } = "";

    public string Author
    {
        get;
        set;
    } = "";

    public string Id
    {
        get;
        set;
    }

    public string Description
    {
        get;
        set;
    } = "";

    public string Version
    {
        get;
        set;
    } = "";

    public string DownloadUrl
    {
        get;
        set;
    } = "";

    public string? PreviewImageUrl
    {
        get;
        set;
    }
}

public class LocalTheme : ReactiveObject
{
    public string Name
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";

    public string Author
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";

    public string Description
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";

    public string Version
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";

    public string Path
    {
        get;
        set;
    } = "";

    public Bitmap? PreviewImage
    {
        get;
        set;
    }
}