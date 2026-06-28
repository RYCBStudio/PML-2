using Avalonia.Interactivity;

namespace Iciclecreek.Terminal
{
    /// <summary>
    /// EventArgs for the ProcessExited event.
    /// </summary>
    public class ProcessExitedEventArgs : RoutedEventArgs
    {
        public int ExitCode { get; }

        public ProcessExitedEventArgs(int exitCode)
        {
            ExitCode = exitCode;
        }
    }
}
