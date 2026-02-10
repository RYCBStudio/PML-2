using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MEFrpLauncherX.Controls;

public partial class DomainsEdit : UserControl, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    public string currentDomain
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public string? CurrentDomain
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public AvaloniaList<string> Domains
    {
        get;
        set;
    }

    public DomainsEdit()
    {
        InitializeComponent();
        Domains = [];
        if (Design.IsDesignMode)
        {
            Domains =
            [
                "example.com",
                "example.org",
                "example.net",
                "example.io",
                "example.edu",
                "example.gov",
                "example.com",
                "example.org",
                "example.net",
                "example.io",
                "example.edu",
                "example.gov",
                "example.com",
                "example.org",
                "example.net",
                "example.io"
            ];
        }

        this.DataContext = this;
    }

    public DomainsEdit(IEnumerable<string> headers)
    {
        InitializeComponent();
        Domains = new AvaloniaList<string>(headers);
        this.DataContext = this;
    }

    private void AddDomain(object? sender, RoutedEventArgs e)
    {
        if (CurrentDomain == currentDomain ||
            Domains.FirstOrDefault(x => x == currentDomain) != null)
        {
            return;
        }

        Domains.Add(currentDomain);
    }

    private void RemoveDomain(object? sender, RoutedEventArgs e)
    {
        Domains.Remove(CurrentDomain);
    }
}