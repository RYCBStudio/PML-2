using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;

namespace MEFrpLauncherX.Core.Controls;

/// <summary>
///     ChipTextBox - Avalonia 11 compatible.
///     - ItemsSource: IEnumerable (will add/remove when it's an IList like ObservableCollection&lt;T&gt;).
///     - ItemTemplate: IDataTemplate to customize chip visuals.
///     - Press Enter in the input to add a chip; Backspace in empty input removes last chip.
///     - Default ItemTemplate in Themes/Generic.xaml shows a Flyout per chip with Remove action.
/// </summary>
public class ChipTextBox : TemplatedControl
{
    private const string PART_InputName = "PART_Input";
    private TextBox _input;

    public ChipTextBox()
    {
        RemoveCommand = new RelayCommand(o => RemoveItem(o));
        AddCommand = new RelayCommand(o =>
        {
            var text = o as string ?? Text;
            if (!string.IsNullOrWhiteSpace(text))
            {
                AddItem(text.Trim());
                Text = string.Empty;
            }
        });
        if (!CanAdd)
        {
            _input?.IsVisible = false;
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_input != null)
        {
            _input.KeyUp -= Input_KeyUp;
        }

        _input = e.NameScope.Find<TextBox>(PART_InputName);

        if (_input != null)
        {
            // Bind the input's Text to the control's Text property
            _input.Bind(TextBox.TextProperty, this.GetObservable(TextProperty));
            _input.KeyUp += Input_KeyUp;
        }
    }

    private void Input_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var text = _input.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                AddItem(text);
                _input.Text = string.Empty;
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Back)
        {
            if (string.IsNullOrEmpty(_input.Text))
            {
                TryRemoveLastItem();
                e.Handled = true;
            }
        }
    }

    private void TryRemoveLastItem()
    {
        if (ItemsSource is IList { Count: > 0 } list)
        {
            list.RemoveAt(list.Count - 1);
        }
    }

    private void AddItem(string text)
    {
        if (ItemsSource is IList list)
        {
            try
            {
                list.Add(ConvertItem(list, text));
            }
            catch
            {
                // Swallow — callers should provide a compatible collection type.
            }
        }
        // If ItemsSource isn't an IList, we don't mutate it. Recommend using ObservableCollection<T>.
    }

    private object ConvertItem(IList list, string text)
    {
        var listType = list.GetType();
        if (listType.IsGenericType)
        {
            var elementType = listType.GetGenericArguments()[0];
            if (elementType == typeof(string) || elementType == typeof(object))
            {
                return text;
            }

            try
            {
                // Attempt conversion (works for primitives and types with TypeConverter)
                return Convert.ChangeType(text, elementType);
            }
            catch
            {
                return text;
            }
        }

        return text;
    }

    private void RemoveItem(object item)
    {
        if (item == null)
        {
            return;
        }

        if (ItemsSource is IList list)
        {
            if (list.Contains(item))
            {
                list.Remove(item);
            }
        }
    }

    #region Styled Properties

    public static readonly StyledProperty<IEnumerable> ItemsSourceProperty =
        AvaloniaProperty.Register<ChipTextBox, IEnumerable>(nameof(ItemsSource));

    public IEnumerable ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly StyledProperty<IDataTemplate> ItemTemplateProperty =
        AvaloniaProperty.Register<ChipTextBox, IDataTemplate>(nameof(ItemTemplate));

    public IDataTemplate ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<ChipTextBox, string>(nameof(Text));

    /// <summary>
    ///     Text shown in the input TextBox.
    /// </summary>
    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    ///     Whether the input TextBox can add a chip by pressing Enter.
    /// </summary>
    public bool CanAdd
    {
        get => GetValue(CanAddProperty);
        set => SetValue(CanAddProperty, value);
    }

    public static readonly StyledProperty<bool> CanAddProperty =
        AvaloniaProperty.Register<ChipTextBox, bool>(nameof(CanAdd), true);

    #endregion

    #region Commands

    public ICommand StopCommand
    {
        get;
    }

    public ICommand RemoveCommand
    {
        get;
    }

    public ICommand AddCommand
    {
        get;
    }

    #endregion
}

// Small ICommand helper
internal class RelayCommand : ICommand
{
    private readonly Func<object, bool> _canExecute;
    private readonly Action<object> _execute;

    public RelayCommand(Action<object> execute) : this(execute, _ => true)
    {
    }

    public RelayCommand(Action<object> execute, Func<object, bool> canExecute)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute ?? throw new ArgumentNullException(nameof(canExecute));
    }

    public bool CanExecute(object parameter) => _canExecute(parameter);

    public void Execute(object parameter) => _execute(parameter);

    public event EventHandler CanExecuteChanged
    {
        add
        {
        }
        remove
        {
        }
    }
}