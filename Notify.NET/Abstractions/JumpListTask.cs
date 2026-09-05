using System;

namespace Notify.NET.Abstractions
{
    /// <summary>
    /// A single entry in an application's jump list (Windows), launcher shortcut menu
    /// (Linux <c>.desktop</c> Actions) or Dock menu (macOS).
    ///
    /// A task represents an action the user can trigger by right-clicking the application's
    /// taskbar/launcher/Dock icon. When clicked, the bundled live-callback layer routes the
    /// task's <see cref="Id"/> back to <see cref="IJumpListHandler.OnTaskActivated"/> in the
    /// already-running instance (see <see cref="IJumpListService"/> for the model).
    /// </summary>
    public sealed class JumpListTask
    {
        /// <summary>
        /// A stable, machine-readable identifier for this task (e.g. <c>"open-library"</c>).
        /// It is passed back to <see cref="IJumpListHandler.OnTaskActivated"/> when the task is
        /// invoked, and is embedded in the relaunch command line on Windows/Linux, so it must
        /// not contain whitespace or characters that need shell quoting. Use letters, digits,
        /// <c>-</c> and <c>_</c>.
        /// </summary>
        public string Id { get; }

        /// <summary>The human-readable label shown in the menu (e.g. <c>"Open Library"</c>).</summary>
        public string Title { get; }

        /// <summary>
        /// Optional tooltip/description. Shown on Windows jump-list tasks on hover.
        /// Ignored on Linux and macOS.
        /// </summary>
        public string? Description { get; }

        /// <summary>
        /// Optional path to an icon. On Windows this is a path to an <c>.ico</c>, <c>.exe</c> or
        /// <c>.dll</c> file whose icon at <see cref="IconIndex"/> is shown next to the task.
        /// On Linux it is an icon name (per the freedesktop icon theme) or absolute path written
        /// into the <c>.desktop</c> Action. Ignored on macOS (Dock menus do not show item icons).
        /// </summary>
        public string? IconPath { get; }

        /// <summary>
        /// The zero-based index of the icon to use within <see cref="IconPath"/> when it refers to
        /// a multi-icon file (e.g. an <c>.exe</c>/<c>.dll</c>). Windows only; defaults to 0.
        /// </summary>
        public int IconIndex { get; }

        /// <param name="id">A stable, whitespace-free identifier passed to the handler when invoked.</param>
        /// <param name="title">The label shown in the menu.</param>
        /// <param name="description">Optional Windows-only tooltip.</param>
        /// <param name="iconPath">Optional icon file (Windows) or icon name/path (Linux).</param>
        /// <param name="iconIndex">Icon index within <paramref name="iconPath"/> (Windows only).</param>
        public JumpListTask(
            string id,
            string title,
            string? description = null,
            string? iconPath = null,
            int iconIndex = 0)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Task id must not be empty.", nameof(id));
            if (HasWhitespace(id))
                throw new ArgumentException("Task id must not contain whitespace.", nameof(id));
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Task title must not be empty.", nameof(title));

            Id = id;
            Title = title;
            Description = description;
            IconPath = iconPath;
            IconIndex = iconIndex;
        }

        private static bool HasWhitespace(string s)
        {
            foreach (char c in s)
                if (char.IsWhiteSpace(c)) return true;
            return false;
        }
    }
}
