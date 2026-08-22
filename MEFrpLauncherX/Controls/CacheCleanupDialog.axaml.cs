using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Media;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Core.Services;

namespace MEFrpLauncherX.Controls;

/// <summary>
///     缓存清理对话框：展示 Cache / Logs 目录大小，按用户选择的保留时长清理旧文件。
/// </summary>
public partial class CacheCleanupDialog : UserControl, INotifyPropertyChanged
{
    public CacheCleanupDialog()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RefreshSizesAsync();
    }

    public string CacheSizeText
    {
        get;
        private set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged();
            Cache.Text = field;
        }
    }

    public string LogsSizeText
    {
        get;
        private set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged();
            LogsCache.Text = field;
        }
    }

    /// <summary>
    ///     当前选择的保留天数；null 表示全部清理。
    /// </summary>
    private int? SelectedDays => DaysSlider.Value == 0 ? null : (int)DaysSlider.Value;

    /// <summary>
    ///     重新统计并刷新 Cache / Logs 目录大小显示。
    /// </summary>
    public async Task RefreshSizesAsync()
    {
        var (cacheSize, logsSize) = await Task.Run(() =>
        (
            ToolboxService.GetDirectorySize(Path.Combine(Core.App.StartupPath, "Cache")),
            ToolboxService.GetDirectorySize(Path.Combine(Core.App.StartupPath, "Logs"))
        ));
        CacheSizeText = ToolboxService.FormatFileSize(cacheSize);
        LogsSizeText = ToolboxService.FormatFileSize(logsSize);
    }

    private async void Clean_Click(object? sender, RoutedEventArgs e)
    {
        if (!CleanButton.IsEnabled)
        {
            return;
        }

        CleanButton.IsEnabled = false;
        CleanProgress.IsVisible = true;
        CleaningText.IsVisible = true;
        ResultText.IsVisible = false;
        try
        {
            var days = SelectedDays;
            var span = days.HasValue ? TimeSpan.FromDays(days.Value) : TimeSpan.MaxValue;
            var (deleted, freed) = await Task.Run(() =>
            {
                var cache = ToolboxService.CleanOldFiles(Path.Combine(Core.App.StartupPath, "Cache"), span);
                var logs = ToolboxService.CleanOldFiles(Path.Combine(Core.App.StartupPath, "Logs"), span);
                return (cache.DeletedCount + logs.DeletedCount, cache.FreedBytes + logs.FreedBytes);
            });
            await RefreshSizesAsync();
            ResultText.Text = deleted > 0
                ? string.Format(Languages.Text_About_ToolBox_ClearCache_Dialog_Done, deleted,
                    ToolboxService.FormatFileSize(freed))
                : Languages.Text_About_ToolBox_ClearCache_Dialog_Nothing;
            SetResultColor("SystemFillColorSuccessBrush");
            ResultText.IsVisible = true;
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Error(ex, port: EnumLogPort.Client, module: EnumLogModule.Main);
            ResultText.Text = Languages.Text_About_ToolBox_ClearCache_Dialog_Failed;
            SetResultColor("SystemFillColorCriticalBrush");
            ResultText.IsVisible = true;
        }
        finally
        {
            CleanButton.IsEnabled = true;
            CleanProgress.IsVisible = false;
            CleaningText.IsVisible = false;
        }
    }

    private void SetResultColor(string resourceKey)
    {
        if (Application.Current?.TryFindResource(resourceKey, App.Current.ActualThemeVariant, out var brush) == true &&
            brush is IBrush b)
        {
            ResultText.Foreground = b;
        }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


    private async void RefreshTrash(object? sender, RangeBaseValueChangedEventArgs e)
    {
        await RefreshSizesAsync();
    }
}

public class DaysToVisibilityConverter : IValueConverter
{
    public static DaysToVisibilityConverter Instance
    {
        get;
    } = new();
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            var v = System.Convert.ToDouble(value);
            return v >= 366;
        }
        catch
        {
            return false;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class DaysToVisibilityConverterReverse : IValueConverter
{
    public static DaysToVisibilityConverterReverse Instance
    {
        get;
    } = new();
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            var v = System.Convert.ToDouble(value);
            return v < 366;
        }
        catch
        {
            return false;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}