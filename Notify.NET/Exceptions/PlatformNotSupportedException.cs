using System;

namespace Notify.NET.Exceptions
{
    /// <summary>
    /// Thrown when <see cref="Abstractions.INotificationService.ShowAsync"/> is called on a platform
    /// where the native notification subsystem is unavailable or could not be initialised.
    /// Check <see cref="Abstractions.INotificationService.IsSupported"/> before calling Show.
    /// </summary>
    public sealed class PlatformNotSupportedException : Exception
    {
        public PlatformNotSupportedException()
            : base("Native notifications are not supported or could not be initialised on this platform.") { }

        public PlatformNotSupportedException(string message) : base(message) { }

        public PlatformNotSupportedException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
