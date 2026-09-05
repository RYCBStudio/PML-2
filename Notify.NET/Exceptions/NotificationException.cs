using System;

namespace Notify.NET.Exceptions
{
    /// <summary>Thrown when the native notification subsystem returns an error.</summary>
    public sealed class NotificationException : Exception
    {
        /// <summary>Platform-specific error code, if available.</summary>
        public int? NativeErrorCode { get; }

        public NotificationException(string message) : base(message) { }

        public NotificationException(string message, int nativeErrorCode)
            : base(message)
        {
            NativeErrorCode = nativeErrorCode;
        }

        public NotificationException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
