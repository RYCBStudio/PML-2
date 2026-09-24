using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using AvaloniaEdit;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Tools;

namespace MEFrpLauncherX.Controls;

public partial class FeedbackForm : UserControl, INotifyPropertyChanged
{
    public FeedbackForm()
    {
        InitializeComponent();
    }

    [EmailAddress(ErrorMessageResourceName = "Text_Certificate_Validation_EmailInvalid",
        ErrorMessageResourceType = typeof(Languages))]
    [Required(ErrorMessageResourceName = "Text_Validation_EmailRequired", ErrorMessageResourceType = typeof(Languages))]
    public string? Email
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    [Required]
    public string? Feedback
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

public sealed class EmailAddressAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value == null)
        {
            ErrorMessage = Languages.Text_Validation_EmailRequired;
            return true;
        }
        if (value is not string text || text.AsSpan().ContainsAny<char>('\r', '\n'))
            return false;
        int num = text.IndexOf('@');
        if (num <= 0 || num == text.Length - 1 || num != text.LastIndexOf('@'))
            return false;
        return EmailValidator.IsValidCommonEmail(text);
    }
}