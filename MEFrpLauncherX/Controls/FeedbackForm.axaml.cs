using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using MEFrpLauncherX.Core.Languages;

namespace MEFrpLauncherX.Controls;

public partial class FeedbackForm : UserControl, INotifyPropertyChanged
{
    public FeedbackForm()
    {
        InitializeComponent();
    }

    [Required(ErrorMessageResourceName = "Text_Validation_EmailRequired", ErrorMessageResourceType = typeof(Languages))]
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

    public new event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}