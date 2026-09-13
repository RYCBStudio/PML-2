using Avalonia.Platform.Storage;

namespace MEFrpLauncherX.Core.Helpers;

public sealed class FileSystemHelper
{
    /// <summary>
    /// 打开文件选择器
    /// </summary>
    /// <param name="title">文件选择器标题</param>
    /// <param name="suggestedFileName">建议的文件名</param>
    /// <param name="suggestedStartLocation">建议的起始位置</param>
    /// <param name="fileTypeFilter">文件类型过滤器</param>
    /// <param name="allowMultiple">是否允许多选</param>
    /// <returns>一个<see cref="IStorageFile"/>的只读列表</returns>
    public static async Task<IReadOnlyList<IStorageFile>> OpenFilePickerAsync(
        string title,
        string suggestedFileName = "",
        string? suggestedStartLocation = "",
        string fileTypeFilter = "",
        bool allowMultiple = false)
    {
        var mainWindow = App.MainWindow;
        if (mainWindow is null)
            return null;
        if (!fileTypeFilter.Contains(Languages.Languages.Text_Global_FileType_All))
        {
            fileTypeFilter += "|" + Languages.Languages.Text_Global_FileType_All + ";*.*";
        }
        // 1. 解析过滤器字符串，构建 FilePickerFileType 列表
        var filters = new List<FilePickerFileType>();
        if (!string.IsNullOrEmpty(fileTypeFilter))
        {
            // 按 '|' 分割，每两个一组构成一个过滤器
            var parts = fileTypeFilter.Split('|');
            for (var i = 0; i < parts.Length; i += 2)
            {
                if (i + 1 >= parts.Length) break; // 忽略不完整的条目

                var description = parts[i].Trim();
                var patterns = parts[i + 1].Trim();

                filters.Add(new FilePickerFileType(description)
                {
                    Patterns =
                    [
                        .. patterns.Split(';', StringSplitOptions.RemoveEmptyEntries)
                            .Select(p => p.Trim())
                    ]
                });
            }
        }

        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = allowMultiple,
            SuggestedFileName = suggestedFileName,
            // 2. 正确赋值 FileTypeFilter
            FileTypeFilter = filters.Count > 0 ? filters : null,
            // 3. SuggestedFileType 必须引用 FileTypeFilter 中的某一项
            SuggestedFileType = filters.FirstOrDefault(),
            SuggestedStartLocation = string.IsNullOrEmpty(suggestedStartLocation)
                ? null
                : await mainWindow?.StorageProvider.TryGetFolderFromPathAsync(suggestedStartLocation)
        };

        var files = await mainWindow?.StorageProvider.OpenFilePickerAsync(options)!;
        return files;
    }

    /// <summary>
    /// 打开文件保存器
    /// </summary>
    /// <param name="title">文件保存器标题</param>
    /// <param name="suggestedFileName">建议的文件名</param>
    /// <param name="suggestedStartLocation">建议的起始位置</param>
    /// <param name="fileTypeFilter"></param>
    /// <returns>Saved <see cref="IStorageFile"/> or null if user canceled the dialog.</returns>
    public static async Task<IStorageFile?> SaveFilePickerAsync(
        string title,
        string suggestedFileName = "",
        string? suggestedStartLocation = "",
        string fileTypeFilter = "")
    {
        var mainWindow = App.MainWindow;
        if (mainWindow is null)
            return null;

        if (!fileTypeFilter.Contains(Languages.Languages.Text_Global_FileType_All))
        {
            fileTypeFilter += "|" + Languages.Languages.Text_Global_FileType_All + ";*.*";
        }
        var storageProvider = mainWindow.StorageProvider;

        // 1. 解析过滤器字符串，构建 FilePickerFileType 列表
        var filters = new List<FilePickerFileType>();
        if (!string.IsNullOrEmpty(fileTypeFilter))
        {
            var parts = fileTypeFilter.Split('|');
            for (var i = 0; i < parts.Length; i += 2)
            {
                if (i + 1 >= parts.Length) break;

                var description = parts[i].Trim();
                var patterns = parts[i + 1].Trim();

                filters.Add(new FilePickerFileType(description)
                {
                    Patterns =
                    [
                        .. patterns.Split(';', StringSplitOptions.RemoveEmptyEntries)
                            .Select(p => p.Trim())
                    ]
                });
            }
        }

        // 2. 计算起始位置
        IStorageFolder? startLocation = null;
        if (!string.IsNullOrEmpty(suggestedStartLocation))
        {
            if (suggestedStartLocation.StartsWith("wellknown:"))
            {
                var name = suggestedStartLocation[10..];
                var folder = Enum.TryParse(typeof(WellKnownFolder), name, true, out var wk)
                    ? (WellKnownFolder)wk
                    : WellKnownFolder.Desktop;

                startLocation = await storageProvider.TryGetWellKnownFolderAsync(folder);
            }
            else
            {
                startLocation = await storageProvider.TryGetFolderFromPathAsync(suggestedStartLocation);
            }
        }

        var options = new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            SuggestedFileType = filters.FirstOrDefault(),
            DefaultExtension = ".png",
            FileTypeChoices = filters,
            ShowOverwritePrompt = true,
            SuggestedStartLocation = startLocation,
        };

        return await storageProvider.SaveFilePickerAsync(options);
    }
}