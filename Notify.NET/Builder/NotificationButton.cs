using System;

namespace Notify.NET.Builder
{
    /// <summary>
    /// An action button displayed inside a notification.
    /// </summary>
    public sealed class NotificationButton
    {
        /// <summary>The label shown on the button.</summary>
        public string Label { get; }

        /// <summary>
        /// Callback invoked when the user clicks this button.
        /// The argument is the platform notification ID.
        /// The callback is invoked on a background thread; marshal to the UI thread if required.
        /// </summary>
        public Action<long>? Callback { get; }

        /// <summary>
        /// An optional machine-readable identifier for this action (used internally by libnotify).
        /// Defaults to a sanitised version of <see cref="Label"/> when not specified.
        /// </summary>
        public string ActionId { get; }

        public NotificationButton(string label, Action<long>? callback = null, string? actionId = null)
        {
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("Button label must not be empty.", nameof(label));

            Label = label;
            Callback = callback;
            ActionId = actionId ?? label.ToLowerInvariant().Replace(' ', '-');
        }
    }
}
