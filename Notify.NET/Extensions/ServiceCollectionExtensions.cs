using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Notify.NET.Abstractions;
using Notify.NET.Platform.Windows;
using Notify.NET.Platform.Linux;
using Notify.NET.Platform.MacOS;

namespace Notify.NET.Extensions
{
    /// <summary>
    /// Extension methods for registering <see cref="INotificationService"/> with an
    /// <see cref="IServiceCollection"/>. The correct platform implementation is selected
    /// automatically at runtime.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <see cref="INotificationService"/> as a singleton, using the
        /// platform-appropriate backend:
        /// <list type="bullet">
        ///   <item><description>Windows → <see cref="WindowsNotificationService"/> (WinToastLib)</description></item>
        ///   <item><description>Linux → <see cref="LinuxNotificationService"/> (libnotify)</description></item>
        ///   <item><description>macOS → <see cref="MacOSNotificationService"/> (UNUserNotificationCenter)</description></item>
        ///   <item><description>Other → <see cref="NullNotificationService"/> (<see cref="INotificationService.IsSupported"/> = false)</description></item>
        /// </list>
        /// </summary>
        /// <param name="services">The service collection to add to.</param>
        /// <param name="configure">Optional delegate to configure <see cref="NotificationOptions"/>.</param>
        public static IServiceCollection AddNotifications(
            this IServiceCollection services,
            Action<NotificationOptions>? configure = null)
        {
            var options = new NotificationOptions();
            configure?.Invoke(options);

            services.AddSingleton(options);

            services.AddSingleton<INotificationService>(sp =>
            {
                var opts = sp.GetRequiredService<NotificationOptions>();
                return CreateService(opts);
            });

            return services;
        }

        /// <summary>
        /// Creates the platform-appropriate <see cref="INotificationService"/> directly
        /// (without a DI container), for use in simple console applications.
        /// </summary>
        public static INotificationService CreateNotificationService(
            Action<NotificationOptions>? configure = null)
        {
            var opts = new NotificationOptions();
            configure?.Invoke(opts);
            return CreateService(opts);
        }

        private static INotificationService CreateService(NotificationOptions opts)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return new WindowsNotificationService(opts.AppName, opts.AppUserModelId, opts.AppIconPath);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return new LinuxNotificationService(opts.AppName);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return new MacOSNotificationService(opts.AppName);

            return new NullNotificationService();
        }

        /// <summary>
        /// Registers <see cref="ITaskbarProgressService"/> as a singleton, using the
        /// platform-appropriate backend:
        /// <list type="bullet">
        ///   <item><description>Windows → <see cref="WindowsTaskbarProgressService"/> (ITaskbarList3)</description></item>
        ///   <item><description>Linux → <see cref="LinuxTaskbarProgressService"/> (Unity LauncherEntry D-Bus)</description></item>
        ///   <item><description>macOS → <see cref="MacOSTaskbarProgressService"/> (Dock tile)</description></item>
        ///   <item><description>Other → <see cref="NullTaskbarProgressService"/> (<see cref="ITaskbarProgressService.IsSupported"/> = false)</description></item>
        /// </list>
        /// </summary>
        /// <param name="services">The service collection to add to.</param>
        /// <param name="configure">Optional delegate to configure <see cref="NotificationOptions"/>.</param>
        public static IServiceCollection AddTaskbarProgress(
            this IServiceCollection services,
            Action<NotificationOptions>? configure = null)
        {
            var options = new NotificationOptions();
            configure?.Invoke(options);

            services.AddSingleton<ITaskbarProgressService>(_ => CreateTaskbarService(options));
            return services;
        }

        /// <summary>
        /// Creates the platform-appropriate <see cref="ITaskbarProgressService"/> directly
        /// (without a DI container), for use in simple console applications.
        /// </summary>
        public static ITaskbarProgressService CreateTaskbarProgressService(
            Action<NotificationOptions>? configure = null)
        {
            var opts = new NotificationOptions();
            configure?.Invoke(opts);
            return CreateTaskbarService(opts);
        }

        private static ITaskbarProgressService CreateTaskbarService(NotificationOptions opts)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return new WindowsTaskbarProgressService();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return new LinuxTaskbarProgressService(
                    opts.DesktopFileId ?? System.Diagnostics.Process.GetCurrentProcess().ProcessName);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return new MacOSTaskbarProgressService();

            return new NullTaskbarProgressService();
        }

        /// <summary>
        /// Registers <see cref="IJumpListService"/> as a singleton, using the
        /// platform-appropriate backend:
        /// <list type="bullet">
        ///   <item><description>Windows → <see cref="WindowsJumpListService"/> (ICustomDestinationList user tasks)</description></item>
        ///   <item><description>Linux → <see cref="LinuxJumpListService"/> (freedesktop.org Desktop Actions)</description></item>
        ///   <item><description>macOS → <see cref="MacOSJumpListService"/> (Dock menu)</description></item>
        ///   <item><description>Other → <see cref="NullJumpListService"/> (<see cref="IJumpListService.IsSupported"/> = false)</description></item>
        /// </list>
        ///
        /// On Windows and Linux a clicked task relaunches the executable with
        /// <c>--notify-jumplist &lt;id&gt;</c>; the bundled single-instance layer forwards the id to the
        /// running primary instance so the handler fires live. The IPC listener and OS registration
        /// are created lazily — only when tasks or a handler are actually configured.
        /// </summary>
        /// <param name="services">The service collection to add to.</param>
        /// <param name="configure">Optional delegate to configure <see cref="NotificationOptions"/>.</param>
        public static IServiceCollection AddJumpList(
            this IServiceCollection services,
            Action<NotificationOptions>? configure = null)
        {
            var options = new NotificationOptions();
            configure?.Invoke(options);

            services.AddSingleton<IJumpListService>(_ => CreateJumpListServiceCore(options));
            return services;
        }

        /// <summary>
        /// Creates the platform-appropriate <see cref="IJumpListService"/> directly
        /// (without a DI container), for use in simple console applications.
        /// </summary>
        public static IJumpListService CreateJumpListService(
            Action<NotificationOptions>? configure = null)
        {
            var opts = new NotificationOptions();
            configure?.Invoke(opts);
            return CreateJumpListServiceCore(opts);
        }

        private static IJumpListService CreateJumpListServiceCore(NotificationOptions opts)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return new WindowsJumpListService(opts.AppUserModelId, opts.ExecutablePath);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return new LinuxJumpListService(
                    opts.AppName,
                    opts.DesktopFileId ?? System.Diagnostics.Process.GetCurrentProcess().ProcessName,
                    opts.ExecutablePath);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return new MacOSJumpListService();

            return new NullJumpListService();
        }
    }

    /// <summary>
    /// Configuration options for the notification service.
    /// Pass to <see cref="ServiceCollectionExtensions.AddNotifications"/> via the configure delegate.
    /// </summary>
    public sealed class NotificationOptions
    {
        /// <summary>
        /// Human-readable application name shown in the notification and Action Centre.
        /// Defaults to the process name.
        /// </summary>
        public string AppName { get; set; } =
            System.Diagnostics.Process.GetCurrentProcess().ProcessName;

        /// <summary>
        /// Windows AppUserModelId (AUMI), e.g. <c>"MyCompany.MyApp"</c>.
        /// Required for notifications to persist in the Windows Action Centre.
        /// The native wrapper creates a Start-Menu shortcut with this AUMI automatically.
        /// Ignored on non-Windows platforms.
        /// </summary>
        public string AppUserModelId { get; set; } =
            System.Diagnostics.Process.GetCurrentProcess().ProcessName;

        /// <summary>
        /// Optional absolute path to an .ico (or .exe/.dll) file whose first icon is stamped onto
        /// the Start-Menu shortcut and shown as the small icon in the top-left corner of every toast
        /// notification from this app. Set once at startup; null uses the host executable's icon.
        /// Windows only — ignored on Linux and macOS.
        /// </summary>
        public string? AppIconPath { get; set; }

        /// <summary>
        /// The application's <c>.desktop</c> file id (with or without the ".desktop" suffix), e.g.
        /// <c>"com.example.MyApp"</c>. Used by the Linux taskbar-progress backend to address the
        /// correct launcher entry via the <c>application://&lt;id&gt;.desktop</c> URI. When null,
        /// the process name is used. Ignored on Windows and macOS.
        /// </summary>
        public string? DesktopFileId { get; set; }

        /// <summary>
        /// Absolute path to the executable a jump-list task relaunches when clicked (Windows and
        /// Linux only). When null, the current process executable is used. For framework-dependent
        /// <c>dotnet</c> apps the auto-detected path may be the shared host rather than your app, so
        /// pass an explicit path in that case. Ignored on macOS (the Dock menu fires live, no relaunch).
        /// </summary>
        public string? ExecutablePath { get; set; }
    }

    /// <summary>
    /// No-op implementation used when the current platform has no supported notification backend.
    /// <see cref="IsSupported"/> is always false; calling <see cref="ShowAsync"/> throws
    /// <see cref="Exceptions.PlatformNotSupportedException"/>.
    /// </summary>
    internal sealed class NullNotificationService : INotificationService
    {
        public bool IsSupported => false;

        public Task<long> ShowAsync(NotificationRequest request, CancellationToken cancellationToken = default)
            => throw new Exceptions.PlatformNotSupportedException();

        public Task HideAsync(long notificationId, CancellationToken cancellationToken = default)
            => throw new Exceptions.PlatformNotSupportedException();

        public void Dispose() { }
    }

    /// <summary>
    /// No-op implementation used when the current platform has no supported taskbar-progress
    /// backend. <see cref="IsSupported"/> is always false and every method is a silent no-op.
    /// </summary>
    internal sealed class NullTaskbarProgressService : ITaskbarProgressService
    {
        public bool IsSupported => false;
        public void SetState(TaskbarProgressState state) { }
        public void SetProgress(ulong completed, ulong total) { }
        public void SetProgress(double fraction) { }
        public void SetWindow(IntPtr windowHandle) { }
        public void Dispose() { }
    }

    /// <summary>
    /// No-op implementation used when the current platform has no supported jump-list backend.
    /// <see cref="IsSupported"/> is always false and every method is a silent no-op.
    /// </summary>
    internal sealed class NullJumpListService : IJumpListService
    {
        public bool IsSupported => false;
        public bool TryHandleActivation(string[] args) => false;
        public void SetHandler(IJumpListHandler? handler) { }
        public void SetTasks(System.Collections.Generic.IEnumerable<JumpListTask> tasks) { }
        public void ClearTasks() { }
        public void Dispose() { }
    }
}
