using System;

namespace Notify.NET.Abstractions
{
    /// <summary>
    /// The visual state of a taskbar/dock/launcher progress indicator.
    /// </summary>
    public enum TaskbarProgressState
    {
        /// <summary>No progress bar is shown (the indicator is cleared).</summary>
        None = 0,

        /// <summary>
        /// A "marquee"/pulsing bar with no specific value, used when the total amount of work
        /// is unknown. Honoured on Windows and macOS; Linux launchers fall back to a 0% bar.
        /// </summary>
        Indeterminate = 1,

        /// <summary>A normal (green, on Windows) progress bar reflecting the current value.</summary>
        Normal = 2,

        /// <summary>
        /// A paused (yellow, on Windows) progress bar. Other platforms render this the same as
        /// <see cref="Normal"/> because they cannot tint the bar.
        /// </summary>
        Paused = 3,

        /// <summary>
        /// An error (red, on Windows) progress bar. On Linux the launcher entry is flagged
        /// "urgent"; macOS renders this the same as <see cref="Normal"/>.
        /// </summary>
        Error = 4
    }

    /// <summary>
    /// Controls the progress indicator on the application's taskbar button (Windows),
    /// launcher entry (Linux) or Dock tile (macOS).
    ///
    /// Capability notes:
    /// <list type="bullet">
    ///   <item><description>
    ///     <b>Windows</b> — uses <c>ITaskbarList3</c>. Requires a top-level window handle (HWND).
    ///     Defaults to the console window (<c>GetConsoleWindow()</c>); call <see cref="SetWindow"/>
    ///     to target a WPF/WinForms main window instead.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Linux</b> — uses the Unity LauncherEntry D-Bus API, honoured by KDE Plasma, Unity,
    ///     Dash-to-Dock, Plank and Latte. Requires the app to ship a <c>.desktop</c> file whose id
    ///     is supplied via <c>NotificationOptions.DesktopFileId</c>.
    ///   </description></item>
    ///   <item><description>
    ///     <b>macOS</b> — draws an <c>NSProgressIndicator</c> on the Dock tile. Only visible for a
    ///     regular (GUI/bundled) application that owns a Dock tile; a bare console process has none.
    ///   </description></item>
    /// </list>
    /// If the indicator is not available on the current platform, <see cref="IsSupported"/> is
    /// <c>false</c> and the methods are no-ops.
    /// </summary>
    public interface ITaskbarProgressService : IDisposable
    {
        /// <summary>
        /// Whether a taskbar/launcher/dock progress indicator is available on this platform.
        /// When <c>false</c>, all other methods are silent no-ops.
        /// </summary>
        bool IsSupported { get; }

        /// <summary>
        /// Sets the visual state of the progress indicator without changing its value.
        /// Use <see cref="TaskbarProgressState.None"/> to clear it.
        /// </summary>
        void SetState(TaskbarProgressState state);

        /// <summary>
        /// Sets the progress value and switches the indicator to
        /// <see cref="TaskbarProgressState.Normal"/> (unless it is currently in an
        /// <see cref="TaskbarProgressState.Error"/> or <see cref="TaskbarProgressState.Paused"/>
        /// state, which are preserved).
        /// </summary>
        /// <param name="completed">The amount of work completed.</param>
        /// <param name="total">The total amount of work. Must be greater than zero.</param>
        void SetProgress(ulong completed, ulong total);

        /// <summary>
        /// Sets the progress as a fraction in the range 0.0–1.0, switching the indicator to
        /// <see cref="TaskbarProgressState.Normal"/> (subject to the same state-preservation rule
        /// as <see cref="SetProgress(ulong,ulong)"/>).
        /// </summary>
        /// <param name="fraction">A value between 0.0 and 1.0 (clamped).</param>
        void SetProgress(double fraction);

        /// <summary>
        /// Windows only: targets a specific top-level window (e.g. a WPF/WinForms main window).
        /// On other platforms this is a no-op. Passing <see cref="IntPtr.Zero"/> reverts to the
        /// console window.
        /// </summary>
        /// <param name="windowHandle">The HWND of the window whose taskbar button to control.</param>
        void SetWindow(IntPtr windowHandle);
    }
}
