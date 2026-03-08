using System.Collections.Generic;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MEFrpLauncherX.Controls;

public partial class HeadersEdit : UserControl
{
    public string currentHeader_Name
    {
        get;
        set;
    }

    public string currentHeader_Value
    {
        get;
        set;
    }

    public RequestHeader? CurrentHeader
    {
        get;
        set;
    }

    public AvaloniaList<RequestHeader> Headers
    {
        get;
        set;
    }

    public HeadersEdit()
    {
        InitializeComponent();
        Headers = [];
        if (Design.IsDesignMode)
        {
            Headers =
            [
                new RequestHeader { Name = "Accept", Value = "text/html" },
                new RequestHeader { Name = "Accept-Language", Value = "en-US" },
                new RequestHeader { Name = "User-Agent", Value = "Mozilla/5.0" },
                new RequestHeader { Name = "Connection", Value = "keep-alive" },
                new RequestHeader { Name = "Cache-Control", Value = "no-cache" },
                new RequestHeader { Name = "Pragma", Value = "no-cache" },
                new RequestHeader { Name = "Upgrade-Insecure-Requests", Value = "1" },
                new RequestHeader { Name = "Accept-Encoding", Value = "gzip, deflate" },
                new RequestHeader { Name = "Accept-Charset", Value = "utf-8" },
                new RequestHeader { Name = "DNT", Value = "1" },
                new RequestHeader { Name = "TE", Value = "Trailers" },
                new RequestHeader { Name = "X-Requested-With", Value = "XMLHttpRequest" },
                new RequestHeader { Name = "X-Forwarded-For", Value = "127.0.0.1" },
                new RequestHeader { Name = "X-Forwarded-Proto", Value = "https" },
                new RequestHeader { Name = "X-Forwarded-Host", Value = "127.0.0.1" },
                new RequestHeader { Name = "X-Forwarded-Port", Value = "443" },
                new RequestHeader { Name = "X-Forwarded-Server", Value = "127.0.0.1" },
                new RequestHeader { Name = "X-Real-IP", Value = "127.0.0.1" },
            ];
        }

        DataContext = this;
    }

    public HeadersEdit(IEnumerable<RequestHeader> headers)
    {
        InitializeComponent();
        Headers = new AvaloniaList<RequestHeader>(headers);
        DataContext = this;
    }

    private void AddHeader(object? sender, RoutedEventArgs e)
    {
        var toAdd = new RequestHeader
        {
            Name = currentHeader_Name,
            Value = currentHeader_Value
        };
        if ((CurrentHeader?.Name == toAdd.Name && CurrentHeader?.Value == toAdd.Value) ||
            Headers.FirstOrDefault(x=>x.Name == toAdd.Name && x.Value == toAdd.Value) != null)
        {
            return;
        }

        Headers.Add(toAdd);
    }

    private void RemoveHeader(object? sender, RoutedEventArgs e)
    {
        Headers.Remove(CurrentHeader);
    }
}

public class RequestHeader
{
    public string Name
    {
        get;
        set;
    }

    public string Value
    {
        get;
        set;
    }
}