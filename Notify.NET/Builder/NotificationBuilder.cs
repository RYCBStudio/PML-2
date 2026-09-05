using System;
using System.Collections.Generic;
using Notify.NET.Abstractions;

namespace Notify.NET.Builder
{
    /// <summary>
    /// Fluent builder for constructing a <see cref="NotificationRequest"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// var request = NotificationBuilder.Create("Update available")
    ///     .WithBody("Version 2.0 is ready to install.")
    ///     .WithImage("/usr/share/icons/my-app.png")
    ///     .AddButton("Install now", id => Installer.Run())
    ///     .AddButton("Remind me later", id => Snooze(id))
    ///     .OnActivated(id => Console.WriteLine($"Notification {id} clicked"))
    ///     .OnDismissed((id, reason) => Console.WriteLine($"Dismissed: {reason}"))
    ///     .Build();
    ///
    /// long id = await notificationService.ShowAsync(request);
    /// </code>
    /// </example>
    public sealed class NotificationBuilder
    {
        private string _title = string.Empty;
        private string? _body;
        private string? _imagePath;
        private string? _heroImagePath;
        private string? _inlineImagePath;
        private string? _attributionText;
        private NotificationImageCropHint _imageCropHint = NotificationImageCropHint.Square;
        private NotificationAudioFile? _audioFile;
        private string? _customAudioPath;
        private readonly List<NotificationButton> _buttons = new List<NotificationButton>();
        private INotificationHandler? _handler;
        private TimeSpan? _expiration;
        private NotificationAudio _audio = NotificationAudio.Default;
        private NotificationUrgency _urgency = NotificationUrgency.Normal;

        // Delegate-based callbacks (converted to INotificationHandler in Build())
        private Action<long>? _onActivated;
        private Action<long, int>? _onButtonActivated;
        private Action<long, DismissReason>? _onDismissed;
        private Action<long>? _onFailed;

        private NotificationBuilder() { }

        /// <summary>Creates a new builder with the specified notification title.</summary>
        public static NotificationBuilder Create(string title)
            => new NotificationBuilder { _title = title };

        /// <summary>Sets the notification title.</summary>
        public NotificationBuilder WithTitle(string title)
        {
            _title = title ?? throw new ArgumentNullException(nameof(title));
            return this;
        }

        /// <summary>Sets the notification body text.</summary>
        public NotificationBuilder WithBody(string body)
        {
            _body = body;
            return this;
        }

        /// <summary>
        /// Sets the absolute path of an image used as the app logo override — the small icon
        /// displayed alongside the notification content. In generic toast templates (when a hero
        /// or inline image is present, or when crop hint is <see cref="NotificationImageCropHint.Circle"/>)
        /// this replaces the default app icon. Windows only — ignored on Linux and macOS.
        /// </summary>
        public NotificationBuilder WithImage(string imagePath)
        {
            _imagePath = imagePath;
            return this;
        }

        /// <summary>
        /// Sets the absolute path of an image to display full-width above the notification title,
        /// preserving the image's aspect ratio.
        /// Mutually exclusive with <see cref="WithInlineImage"/>; if both are set the inline image takes precedence.
        /// Windows only — ignored on Linux and macOS.
        /// </summary>
        public NotificationBuilder WithHeroImage(string imagePath)
        {
            _heroImagePath = imagePath;
            return this;
        }

        /// <summary>
        /// Sets the absolute path of an image displayed inline inside the notification body.
        /// Takes precedence over <see cref="WithHeroImage"/> when both are set.
        /// Windows only — ignored on Linux and macOS.
        /// </summary>
        public NotificationBuilder WithInlineImage(string imagePath)
        {
            _inlineImagePath = imagePath;
            return this;
        }

        /// <summary>
        /// Sets small attribution text shown at the bottom of the notification (e.g. a source name or URL).
        /// Windows only — ignored on Linux and macOS.
        /// </summary>
        public NotificationBuilder WithAttributionText(string text)
        {
            _attributionText = text;
            return this;
        }

        /// <summary>
        /// Controls how <see cref="WithImage"/> is cropped when displayed as the app logo override.
        /// <see cref="NotificationImageCropHint.Circle"/> also forces the toast into generic template
        /// mode, which enables hero/inline images and attribution text.
        /// Windows only — ignored on Linux and macOS.
        /// </summary>
        public NotificationBuilder WithImageCropHint(NotificationImageCropHint cropHint)
        {
            _imageCropHint = cropHint;
            return this;
        }

        /// <summary>
        /// Selects a specific Windows system notification sound. Overrides the default sound
        /// selection while still respecting the <see cref="WithAudio"/> loop/silence setting.
        /// Ignored when <see cref="WithCustomAudioPath"/> is also set.
        /// Windows only — ignored on Linux and macOS.
        /// </summary>
        public NotificationBuilder WithAudioFile(NotificationAudioFile audioFile)
        {
            _audioFile = audioFile;
            return this;
        }

        /// <summary>
        /// Sets a custom audio URI (e.g. <c>ms-appx:///sounds/alert.mp3</c> or a
        /// <c>ms-winsoundevent:</c> URI). Takes precedence over <see cref="WithAudioFile"/>.
        /// Windows only — ignored on Linux and macOS.
        /// </summary>
        public NotificationBuilder WithCustomAudioPath(string audioPath)
        {
            _customAudioPath = audioPath;
            return this;
        }

        /// <summary>Adds an action button with an optional click callback.</summary>
        /// <param name="label">Text shown on the button.</param>
        /// <param name="callback">Called with the notification ID when the button is clicked.</param>
        /// <param name="actionId">Optional machine-readable action identifier.</param>
        public NotificationBuilder AddButton(string label, Action<long>? callback = null, string? actionId = null)
        {
            _buttons.Add(new NotificationButton(label, callback, actionId));
            return this;
        }

        /// <summary>Adds a pre-constructed button.</summary>
        public NotificationBuilder AddButton(NotificationButton button)
        {
            _buttons.Add(button ?? throw new ArgumentNullException(nameof(button)));
            return this;
        }

        /// <summary>
        /// Attaches an interface-based handler for all notification lifecycle events.
        /// This takes priority over any delegate-based callbacks registered via
        /// <see cref="OnActivated"/>, <see cref="OnDismissed"/>, or <see cref="OnFailed"/>.
        /// </summary>
        public NotificationBuilder WithHandler(INotificationHandler handler)
        {
            _handler = handler;
            return this;
        }

        /// <summary>Registers a callback for when the notification body is clicked.</summary>
        public NotificationBuilder OnActivated(Action<long> callback)
        {
            _onActivated = callback;
            return this;
        }

        /// <summary>Registers a callback for when a specific action button is clicked.</summary>
        public NotificationBuilder OnButtonActivated(Action<long, int> callback)
        {
            _onButtonActivated = callback;
            return this;
        }

        /// <summary>Registers a callback for when the notification is dismissed.</summary>
        public NotificationBuilder OnDismissed(Action<long, DismissReason> callback)
        {
            _onDismissed = callback;
            return this;
        }

        /// <summary>Registers a callback for when the notification fails to display.</summary>
        public NotificationBuilder OnFailed(Action<long> callback)
        {
            _onFailed = callback;
            return this;
        }

        /// <summary>
        /// Sets how long the notification remains visible before auto-dismissal.
        /// Pass <see cref="TimeSpan.Zero"/> or null to use the platform default.
        /// </summary>
        public NotificationBuilder WithExpiration(TimeSpan expiration)
        {
            _expiration = expiration == TimeSpan.Zero ? (TimeSpan?)null : expiration;
            return this;
        }

        /// <summary>Controls the sound played when the notification appears (Windows only).</summary>
        public NotificationBuilder WithAudio(NotificationAudio audio)
        {
            _audio = audio;
            return this;
        }

        /// <summary>Sets the urgency/scenario which may affect how the platform presents the notification.</summary>
        public NotificationBuilder WithUrgency(NotificationUrgency urgency)
        {
            _urgency = urgency;
            return this;
        }

        /// <summary>
        /// Constructs the immutable <see cref="NotificationRequest"/>.
        /// Throws <see cref="InvalidOperationException"/> if <see cref="WithTitle"/> has not been set.
        /// </summary>
        public NotificationRequest Build()
        {
            if (string.IsNullOrWhiteSpace(_title))
                throw new InvalidOperationException("Notification title must be set before calling Build().");

            // If an explicit INotificationHandler was provided, use it directly.
            // Otherwise, if any delegate callbacks were registered, wrap them.
            INotificationHandler? handler = _handler;
            if (handler == null && (_onActivated != null || _onButtonActivated != null || _onDismissed != null || _onFailed != null))
            {
                handler = new DelegateNotificationHandler(_onActivated, _onButtonActivated, _onDismissed, _onFailed);
            }

            return new NotificationRequest(
                title: _title,
                body: _body,
                imagePath: _imagePath,
                heroImagePath: _heroImagePath,
                inlineImagePath: _inlineImagePath,
                attributionText: _attributionText,
                imageCropHint: _imageCropHint,
                audioFile: _audioFile,
                customAudioPath: _customAudioPath,
                buttons: _buttons.AsReadOnly(),
                handler: handler,
                expiration: _expiration,
                audio: _audio,
                urgency: _urgency);
        }

        // Internal adapter that bridges the delegate callbacks to INotificationHandler.
        private sealed class DelegateNotificationHandler : INotificationHandler
        {
            private readonly Action<long>? _activated;
            private readonly Action<long, int>? _buttonActivated;
            private readonly Action<long, DismissReason>? _dismissed;
            private readonly Action<long>? _failed;

            public DelegateNotificationHandler(
                Action<long>? activated,
                Action<long, int>? buttonActivated,
                Action<long, DismissReason>? dismissed,
                Action<long>? failed)
            {
                _activated = activated;
                _buttonActivated = buttonActivated;
                _dismissed = dismissed;
                _failed = failed;
            }

            public void OnActivated(long id) => _activated?.Invoke(id);
            public void OnButtonActivated(long id, int idx) => _buttonActivated?.Invoke(id, idx);
            public void OnDismissed(long id, DismissReason reason) => _dismissed?.Invoke(id, reason);
            public void OnFailed(long id) => _failed?.Invoke(id);
        }
    }
}
