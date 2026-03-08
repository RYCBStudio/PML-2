using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;

namespace MEFrpLauncherX.Controls;

/// <summary>
/// TypingControl.xaml 的交互逻辑
/// </summary>
public partial class TypingControl : UserControl
{
    public static readonly DirectProperty<TypingControl, string> DisplayingTextProperty = AvaloniaProperty.RegisterDirect<TypingControl, string>(
        nameof(DisplayingText), o => o.DisplayingText, (o, v) => o.DisplayingText = v);

    public string DisplayingText
    {
        get;
        set => SetAndRaise(DisplayingTextProperty, ref field, value);
    }

    public static readonly DirectProperty<TypingControl, string> TextProperty = AvaloniaProperty.RegisterDirect<TypingControl, string>(
        nameof(Text), o => o.Text, (o, v) => o.Text = v);

    public string Text
    {
        get;
        set => SetAndRaise(TextProperty, ref field, value);
    }

    public static readonly DirectProperty<TypingControl, bool> IsBusyProperty = AvaloniaProperty.RegisterDirect<TypingControl, bool>(
        nameof(IsBusy), o => o.IsBusy, (o, v) => o.IsBusy = v);

    public bool IsBusy
    {
        get;
        set => SetAndRaise(IsBusyProperty, ref field, value);
    }

    public static readonly DirectProperty<TypingControl, int> DisplayingIndexProperty = AvaloniaProperty.RegisterDirect<TypingControl, int>(
        nameof(DisplayingIndex), o => o.DisplayingIndex, (o, v) => o.DisplayingIndex = v);

    public int DisplayingIndex
    {
        get;
        set => SetAndRaise(DisplayingIndexProperty, ref field, value);
    }

    public static readonly DirectProperty<TypingControl, bool> IsTextVisibleProperty = AvaloniaProperty.RegisterDirect<TypingControl, bool>(
        nameof(IsTextVisible), o => o.IsTextVisible, (o, v) => o.IsTextVisible = v);

    public bool IsTextVisible
    {
        get;
        set => SetAndRaise(IsTextVisibleProperty, ref field, value);
    }

    private bool _isFirstUpdate = true;

    public TypingControl()
    {
        this.GetObservable(TextProperty).Skip(1).Subscribe(_ => UpdateText());
        InitializeComponent();
        AttachedToVisualTree += (sender, args) => UpdateText();
    }

    private async void UpdateText()
    {
        // TODO: 动画
        IsBusy = true;
        if (!_isFirstUpdate)
        {
            DisplayingText = "";
            await Task.Delay(TimeSpan.FromMilliseconds(150));
            for (int i = 0; i < Text.Length; i++)
            {
                DisplayingText = Text[..i] + ((i / 10) % 2 == 0 ? "_" : "");
                await Task.Delay(TimeSpan.FromMilliseconds(40));
            }
        }

        _isFirstUpdate = false;

        DisplayingText = Text;  
        IsBusy = false;
    }
}
