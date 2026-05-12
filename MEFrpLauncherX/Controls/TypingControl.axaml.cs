using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;

namespace MEFrpLauncherX.Controls;

/// <summary>
///     TypingControl.xaml 的交互逻辑
/// </summary>
public partial class TypingControl : UserControl
{
    public static readonly DirectProperty<TypingControl, string> DisplayingTextProperty =
        AvaloniaProperty.RegisterDirect<TypingControl, string>(
            nameof(DisplayingText), o => o.DisplayingText, (o, v) => o.DisplayingText = v);

    public static readonly DirectProperty<TypingControl, string> TextProperty =
        AvaloniaProperty.RegisterDirect<TypingControl, string>(
            nameof(Text), o => o.Text, (o, v) => o.Text = v);

    public static readonly DirectProperty<TypingControl, bool> IsBusyProperty =
        AvaloniaProperty.RegisterDirect<TypingControl, bool>(
            nameof(IsBusy), o => o.IsBusy, (o, v) => o.IsBusy = v);

    public static readonly DirectProperty<TypingControl, int> DisplayingIndexProperty =
        AvaloniaProperty.RegisterDirect<TypingControl, int>(
            nameof(DisplayingIndex), o => o.DisplayingIndex, (o, v) => o.DisplayingIndex = v);

    public static readonly DirectProperty<TypingControl, bool> IsTextVisibleProperty =
        AvaloniaProperty.RegisterDirect<TypingControl, bool>(
            nameof(IsTextVisible), o => o.IsTextVisible, (o, v) => o.IsTextVisible = v);

    private static readonly Random _random = new();

    private static readonly char[] _specialChars =
        "AaBbCcDdEeFfGgHhIiJjKkLlMmNnOoPpQqRrSsTtUuVvWwXxYyZz0123456789!@#$%^&*(){}[]|\\:;.,?/".ToCharArray();

    private static readonly Range _chineseRange = new('\u4e00', '\u9fA5');

    private CancellationTokenSource _currentAnimationCts;

    private bool _isFirstUpdate = true;

    private string _previousText;

    public TypingControl()
    {
        this.GetObservable(TextProperty).Skip(1).Subscribe(_ => UpdateText());
        InitializeComponent();
        AttachedToVisualTree += (sender, args) => UpdateText();
    }

    public string DisplayingText
    {
        get;
        set => SetAndRaise(DisplayingTextProperty, ref field, value);
    }

    public string Text
    {
        get;
        set => SetAndRaise(TextProperty, ref field, value);
    }

    public bool IsBusy
    {
        get;
        set => SetAndRaise(IsBusyProperty, ref field, value);
    }

    public int DisplayingIndex
    {
        get;
        set => SetAndRaise(DisplayingIndexProperty, ref field, value);
    }

    public bool IsTextVisible
    {
        get;
        set => SetAndRaise(IsTextVisibleProperty, ref field, value);
    }

    private async void UpdateText()
    {
        if (Text == _previousText)
        {
            return;
        }

        try
        {
            await _currentAnimationCts.CancelAsync();
        }
        catch
        {
        }

        _currentAnimationCts = new CancellationTokenSource();
        var ct = _currentAnimationCts.Token;

        var isEmpty = string.IsNullOrEmpty(Text) || Text == "   —— 「」";

        try
        {
            if (!isEmpty)
            {
                _previousText = Text;
                IsBusy = true;
                await Task.Delay(TimeSpan.FromMilliseconds(150), ct);

                for (var i = 0; i < Text.Length; i++)
                {
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    var currentChar = Text[i];
                    string placeholder;

                    if (currentChar >= '\u4e00' && currentChar <= '\u9fA5')
                    {
                        // 汉字：随机显示一个汉字
                        var chineseIndex = _random.Next(_chineseRange.Start.Value, _chineseRange.End.Value + 1);
                        placeholder = ((char)chineseIndex).ToString();
                    }
                    else if ((currentChar >= 'A' && currentChar <= 'Z') ||
                             (currentChar >= 'a' && currentChar <= 'z') ||
                             (currentChar >= '0' && currentChar <= '9'))
                    {
                        // 英文字母或数字：随机显示特殊字符
                        placeholder = _specialChars[_random.Next(_specialChars.Length)].ToString();
                    }
                    else
                    {
                        // 其他字符：显示 _
                        placeholder = "_";
                    }

                    DisplayingText = Text[..i] + placeholder;
                    await Task.Delay(TimeSpan.FromMilliseconds(40), ct);
                }
            }
            else
            {
                IsBusy = true;
                await Task.Delay(TimeSpan.FromMilliseconds(100), ct);

                for (var i = DisplayingText?.Length ?? 0; i > 0; i--)
                {
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    try
                    {
                        DisplayingText = DisplayingText?[..(i - 1)] ?? "_";
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        DisplayingText = Text;
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(20), ct);
                }

                _previousText = Text;
            }

            _isFirstUpdate = false;

            DisplayingText = Text;
            IsBusy = false;
        }
        catch (OperationCanceledException)
        {
        }
    }
}