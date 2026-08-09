using Avalonia.Interactivity;

namespace Iciclecreek.TerminalWindow
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
