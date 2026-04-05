using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace MEFrpLauncherX.Core.Controls;

public partial class NoticeView : UserControl
{
    public NoticeContent? Notice
    {
        get;
        set;
    }

    public NoticeView()
    {
        InitializeComponent();
        if (Design.IsDesignMode)
        {
            Notice = new NoticeContent();
        }

        DataContext = Notice;
    }

    public NoticeView(NoticeContent notice, string markdown)
    {
        InitializeComponent();
        Notice = notice;
        DataContext = this;
        MarkdownRender.Value = markdown;
    }
}

public class TypeToReadableChineseConverter : IValueConverter
{

    public static TypeToReadableChineseConverter Instance
    {
        get;
    } = new();
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string _type
            ? _type.ToLower() switch
            {
                "maintenance" => "维护",
                "notice" => "公告",
                "update" => "新版本",
                _ => _type
            }
            : value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}