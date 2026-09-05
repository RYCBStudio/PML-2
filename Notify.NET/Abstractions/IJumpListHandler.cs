namespace Notify.NET.Abstractions
{
    /// <summary>
    /// Receives activation events when the user clicks an entry in the application's jump list,
    /// launcher shortcut menu or Dock menu.
    /// </summary>
    public interface IJumpListHandler
    {
        /// <summary>
        /// Called when the user invokes a jump-list task.
        ///
        /// On Windows and Linux the click relaunches the executable, and the bundled single-instance
        /// layer forwards the activation to the already-running primary instance, where this method
        /// is invoked. On a cold start (no primary instance was running) the activation is replayed
        /// once a handler has been registered. On macOS the Dock-menu click invokes this method
        /// directly, in-process.
        ///
        /// This callback may be invoked on a background thread; marshal to the UI thread if required.
        /// </summary>
        /// <param name="taskId">The <see cref="JumpListTask.Id"/> of the task that was clicked.</param>
        void OnTaskActivated(string taskId);
    }
}
