using System;
using System.Collections.Generic;
using Notify.NET.Builder;

namespace Notify.NET.Abstractions
{
    /// <summary>
    /// Immutable description of a notification to be displayed.
    /// Construct instances via <see cref="NotificationBuilder"/>.
    /// </summary>
    public sealed class NotificationRequest
    {
        /// <summary>The primary heading of the notification.</summary>
        public string Title { get; }

        /// <summary>Optional body text shown beneath the title.</summary>
        public string? Body { get; }

        /// <summary>
        /// Absolute path to an image used as the app logo override — the small icon shown
        /// alongside the notification content. In generic toast templates (i.e. when a hero
        /// or inline image is also present, or when <see cref="ImageCropHint"/> is
        /// <see cref="NotificationImageCropHint.Circle"/>) this image replaces the default
        /// app icon. Windows only — ignored on Linux and macOS.
        /// </summary>
        public string? ImagePath { get; }

        /// <summary>
        /// Absolute path to an image file displayed full-width above the title, preserving aspect ratio.
        /// Mutually exclusive with <see cref="InlineImagePath"/> — if both are set, the inline image takes precedence.
        /// Windows only — ignored on Linux and macOS.
        /// </summary>
        public string? HeroImagePath { get; }

        /// <summary>
        /// Absolute path to an image file displayed inline inside the notification body.
        /// Takes precedence over <see cref="HeroImagePath"/> when both are set.
        /// Windows only — ignored on Linux and macOS.
        /// </summary>
        public string? InlineImagePath { get; }

        /// <summary>
        /// Small attribution text shown at the bottom of the notification (e.g. a source name).
        /// Windows only — ignored on Linux and macOS.
        /// </summary>
        public string? AttributionText { get; }

        /// <summary>
        /// Controls how <see cref="ImagePath"/> is cropped. Defaults to <see cref="NotificationImageCropHint.Square"/>.
        /// Windows only — ignored on Linux and macOS.
        /// </summary>
        public NotificationImageCropHint ImageCropHint { get; }

        /// <summary>
        /// A specific Windows system notification sound to play, independent of
        /// <see cref="Audio"/>. When set, overrides the default sound selection.
        /// Ignored if <see cref="CustomAudioPath"/> is also set.
        /// Windows only — ignored on Linux and macOS.
        /// </summary>
        public NotificationAudioFile? AudioFile { get; }

        /// <summary>
        /// A custom audio URI (e.g. <c>ms-appx:///sounds/alert.mp3</c> or a
        /// <c>ms-winsoundevent:</c> URI). When set, takes precedence over <see cref="AudioFile"/>.
        /// Windows only — ignored on Linux and macOS.
        /// </summary>
        public string? CustomAudioPath { get; }

        /// <summary>Action buttons to display. Maximum platform limits apply (typically 5 on Windows, varies on Linux).</summary>
        public IReadOnlyList<NotificationButton> Buttons { get; }

        /// <summary>Optional interface-based handler for notification lifecycle events.</summary>
        public INotificationHandler? Handler { get; }

        /// <summary>How long to display the notification before it expires automatically. Null means use the platform default.</summary>
        public TimeSpan? Expiration { get; }

        /// <summary>Audio behaviour when the notification appears.</summary>
        public NotificationAudio Audio { get; }

        /// <summary>The urgency/scenario of the notification, which may affect how the platform presents it.</summary>
        public NotificationUrgency Urgency { get; }

        internal NotificationRequest(
            string title,
            string? body,
            string? imagePath,
            string? heroImagePath,
            string? inlineImagePath,
            string? attributionText,
            NotificationImageCropHint imageCropHint,
            NotificationAudioFile? audioFile,
            string? customAudioPath,
            IReadOnlyList<NotificationButton> buttons,
            INotificationHandler? handler,
            TimeSpan? expiration,
            NotificationAudio audio,
            NotificationUrgency urgency)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Notification title must not be empty.", nameof(title));

            Title = title;
            Body = body;
            ImagePath = imagePath;
            HeroImagePath = heroImagePath;
            InlineImagePath = inlineImagePath;
            AttributionText = attributionText;
            ImageCropHint = imageCropHint;
            AudioFile = audioFile;
            CustomAudioPath = customAudioPath;
            Buttons = buttons;
            Handler = handler;
            Expiration = expiration;
            Audio = audio;
            Urgency = urgency;
        }
    }

    /// <summary>
    /// Controls how <see cref="NotificationRequest.ImagePath"/> is cropped when displayed as the app logo override.
    /// Windows only.
    /// </summary>
    public enum NotificationImageCropHint
    {
        /// <summary>Display the image uncropped (square).</summary>
        Square = 0,
        /// <summary>Crop the image into a circle.</summary>
        Circle = 1
    }

    /// <summary>
    /// Selects a Windows system notification sound.
    /// Set on <see cref="NotificationRequest.AudioFile"/> independently of
    /// <see cref="NotificationAudio"/> (which controls looping/silence behaviour).
    /// </summary>
    public enum NotificationAudioFile
    {
        /// <summary>The generic default notification sound.</summary>
        Default = 0,
        /// <summary>Instant message sound.</summary>
        IM = 1,
        /// <summary>New mail sound.</summary>
        Mail = 2,
        /// <summary>Reminder sound.</summary>
        Reminder = 3,
        /// <summary>SMS / text message sound.</summary>
        SMS = 4,
        /// <summary>Looping alarm sound (variant 1).</summary>
        Alarm = 5,
        /// <summary>Looping alarm sound (variant 2).</summary>
        Alarm2 = 6,
        /// <summary>Looping alarm sound (variant 3).</summary>
        Alarm3 = 7,
        /// <summary>Looping alarm sound (variant 4).</summary>
        Alarm4 = 8,
        /// <summary>Looping alarm sound (variant 5).</summary>
        Alarm5 = 9,
        /// <summary>Looping alarm sound (variant 6).</summary>
        Alarm6 = 10,
        /// <summary>Looping alarm sound (variant 7).</summary>
        Alarm7 = 11,
        /// <summary>Looping alarm sound (variant 8).</summary>
        Alarm8 = 12,
        /// <summary>Looping alarm sound (variant 9).</summary>
        Alarm9 = 13,
        /// <summary>Looping alarm sound (variant 10).</summary>
        Alarm10 = 14,
        /// <summary>Looping incoming-call sound (variant 1).</summary>
        Call = 15,
        /// <summary>Looping incoming-call sound (variant 2).</summary>
        Call1 = 16,
        /// <summary>Looping incoming-call sound (variant 3).</summary>
        Call2 = 17,
        /// <summary>Looping incoming-call sound (variant 4).</summary>
        Call3 = 18,
        /// <summary>Looping incoming-call sound (variant 5).</summary>
        Call4 = 19,
        /// <summary>Looping incoming-call sound (variant 6).</summary>
        Call5 = 20,
        /// <summary>Looping incoming-call sound (variant 7).</summary>
        Call6 = 21,
        /// <summary>Looping incoming-call sound (variant 8).</summary>
        Call7 = 22,
        /// <summary>Looping incoming-call sound (variant 9).</summary>
        Call8 = 23,
        /// <summary>Looping incoming-call sound (variant 10).</summary>
        Call9 = 24,
        /// <summary>Looping incoming-call sound (variant 11).</summary>
        Call10 = 25
    }

    /// <summary>Controls the audio played when the notification is shown (Windows only; Linux ignores this).</summary>
    public enum NotificationAudio
    {
        /// <summary>Play the platform default notification sound.</summary>
        Default,
        /// <summary>Display silently with no sound.</summary>
        Silent,
        /// <summary>Loop the notification sound until the notification is dismissed.</summary>
        Loop
    }

    /// <summary>Maps to the notification urgency/scenario on each platform.</summary>
    public enum NotificationUrgency
    {
        /// <summary>Standard informational notification.</summary>
        Normal,
        /// <summary>Low-priority; the platform may suppress or delay it.</summary>
        Low,
        /// <summary>High-priority; may bypass Do Not Disturb on some platforms.</summary>
        Critical,
        /// <summary>Alarm scenario (Windows) — may produce a full-screen interrupt.</summary>
        Alarm,
        /// <summary>Reminder scenario (Windows).</summary>
        Reminder
    }

    /// <summary>Reason a notification was dismissed.</summary>
    public enum DismissReason
    {
        /// <summary>The user explicitly dismissed the notification.</summary>
        UserCancelled,
        /// <summary>The notification timed out / expired.</summary>
        TimedOut,
        /// <summary>The application programmatically hid the notification.</summary>
        ApplicationHidden,
        /// <summary>Dismissed for an unspecified or platform-specific reason.</summary>
        Unknown
    }
}
