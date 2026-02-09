using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using Avalonia.Controls;

namespace MEFrpLauncherX.Controls;

public partial class FeedbackForm : UserControl, INotifyPropertyChanged
{
    [Required(ErrorMessage = "请填写您的 ME Frp 邮箱")]
    public string Email
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    [Required]
    public string Feedback
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public FeedbackForm()
    {
        InitializeComponent();
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}