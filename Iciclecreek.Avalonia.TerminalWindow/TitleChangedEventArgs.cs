using Avalonia.Interactivity;

namespace Iciclecreek.Terminal
{
    /// <summary>
    /// EventArgs for the TitleChanged event.
    /// </summary>
    public class TitleChangedEventArgs : RoutedEventArgs
    {
        public string Title { get; }

        public TitleChangedEventArgs(string title)
        {
            Title = title;
        }
    }
}
