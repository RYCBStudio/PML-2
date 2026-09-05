# Notify.NET

A cross-platform .NET Standard library for displaying OS notifications. Provides a single
fluent API that dispatches to the native notification system on each supported platform.

| Platform | Backend | Minimum OS |
|----------|---------|------------|
| Windows  | [WinToastLib](https://github.com/mohabouje/WinToast) via a thin C++ wrapper DLL | Windows 8 |
| Linux    | libnotify via P/Invoke | Any distribution with a D-Bus notification daemon |
| macOS    | UNUserNotificationCenter via a thin Objective-C wrapper dylib | macOS 10.14 (Mojave) |

---

## Installation

```
dotnet add package Notify.NET
```

The NuGet package includes the pre-compiled native libraries for all supported platforms.
No separate native installation is required.

---

## Quick start

```csharp
using Notify.NET.Builder;
using Notify.NET.Extensions;

using var service = ServiceCollectionExtensions.CreateNotificationService(opts =>
{
    opts.AppName        = "My App";
    opts.AppUserModelId = "MyCompany.MyApp";  // Windows only; ignored on other platforms
});

if (service.IsSupported)
{
    long id = await service.ShowAsync(
        NotificationBuilder.Create("Hello")
            .WithBody("Notify.NET is working.")
            .Build());
}
```

---

## Creating the service

### Without a DI container

```csharp
using var service = ServiceCollectionExtensions.CreateNotificationService(opts =>
{
    opts.AppName        = "My App";
    opts.AppUserModelId = "MyCompany.MyApp";
});
```

`CreateNotificationService` selects the correct implementation for the current OS
automatically. On unsupported platforms it returns a no-op service where `IsSupported`
is `false`.

### With Microsoft.Extensions.DependencyInjection

```csharp
services.AddNotifications(opts =>
{
    opts.AppName        = "My App";
    opts.AppUserModelId = "MyCompany.MyApp";
});
```

This registers `INotificationService` as a singleton. Resolve it normally:

```csharp
var service = provider.GetRequiredService<INotificationService>();
```

### Checking platform support

Always check `IsSupported` before calling `ShowAsync`. On unsupported platforms or when
initialisation fails (e.g. the user denied notification permission on macOS), `ShowAsync`
throws `PlatformNotSupportedException`.

```csharp
if (!service.IsSupported)
{
    Console.WriteLine("Notifications are not available on this platform.");
    return;
}
```

---

## Builder API

All notification configuration is done through `NotificationBuilder`. The `Build()` call
produces an immutable `NotificationRequest` that can be passed to `ShowAsync`.

### Title and body

```csharp
var request = NotificationBuilder.Create("Title goes here")
    .WithBody("Optional body text goes here.")
    .Build();
```

### Image

Pass an absolute path to an image file. Relative paths are resolved to absolute paths
at call time; if the file does not exist the notification is shown without an image
rather than failing.

```csharp
var request = NotificationBuilder.Create("New photo")
    .WithBody("A photo has arrived.")
    .WithImage("/home/user/photos/latest.jpg")
    .Build();
```

Supported formats depend on the platform (PNG and JPEG work on all three).

### Action buttons

Up to five action buttons can be added. Each button has a label and an optional click
callback that receives the notification ID.

```csharp
var request = NotificationBuilder.Create("Update available")
    .WithBody("Version 2.0 is ready.")
    .AddButton("Install now",      id => Installer.Run())
    .AddButton("Remind me later",  id => ScheduleReminder())
    .AddButton("Skip this version", null)
    .Build();
```

### Lifecycle callbacks

Register delegates for specific notification events:

```csharp
var request = NotificationBuilder.Create("Download complete")
    .WithBody("report.pdf has been saved.")
    .OnActivated(id => OpenFile("report.pdf"))
    .OnDismissed((id, reason) =>
    {
        if (reason == DismissReason.UserCancelled)
            Console.WriteLine("User dismissed the notification.");
    })
    .OnFailed(id => Console.WriteLine($"Notification {id} could not be displayed."))
    .Build();
```

For more complex cases, implement `INotificationHandler` and attach it with `WithHandler`:

```csharp
public sealed class DownloadHandler : INotificationHandler
{
    public void OnActivated(long id)             => OpenDownloadFolder();
    public void OnButtonActivated(long id, int i) => HandleButton(i);
    public void OnDismissed(long id, DismissReason r) { }
    public void OnFailed(long id)                => LogError(id);
}

var request = NotificationBuilder.Create("Download complete")
    .WithHandler(new DownloadHandler())
    .Build();
```

`WithHandler` takes precedence over any delegate callbacks registered on the same builder.

### Urgency

```csharp
// Low priority — the platform may suppress or delay it
NotificationBuilder.Create("FYI").WithUrgency(NotificationUrgency.Low)

// Critical — bypasses Do Not Disturb where the platform supports it
NotificationBuilder.Create("Disk full").WithUrgency(NotificationUrgency.Critical)

// Windows-specific scenarios
NotificationBuilder.Create("Meeting in 5 minutes").WithUrgency(NotificationUrgency.Reminder)
NotificationBuilder.Create("Incoming call").WithUrgency(NotificationUrgency.Alarm)
```

| Value | Windows | Linux | macOS |
|-------|---------|-------|-------|
| `Normal` | Default scenario | Normal urgency | Active interruption level |
| `Low` | Default scenario | Low urgency | Passive interruption level |
| `Critical` | Default scenario | Critical urgency | Critical interruption level (macOS 12+) |
| `Alarm` | Alarm scenario | Critical urgency | Critical interruption level (macOS 12+) |
| `Reminder` | Reminder scenario | Normal urgency | Active interruption level |

### Audio

```csharp
NotificationBuilder.Create("Alert").WithAudio(NotificationAudio.Silent)
NotificationBuilder.Create("Alarm").WithAudio(NotificationAudio.Loop)   // Windows only
```

| Value | Windows | Linux | macOS |
|-------|---------|-------|-------|
| `Default` | System notification sound | Controlled by daemon | System notification sound |
| `Silent` | No sound | No sound | No sound |
| `Loop` | Sound loops until dismissed | Treated as default | Treated as default |

### Expiration

Sets how long the notification is visible before it auto-dismisses. Pass `null` or omit
the call to use the platform default.

```csharp
NotificationBuilder.Create("Reminder")
    .WithExpiration(TimeSpan.FromSeconds(10))
    .Build();
```

Note: `UNUserNotificationCenter` on macOS does not expose a per-notification timeout API;
this value is stored in the request but has no effect on macOS.

---

## Showing and hiding notifications

`ShowAsync` returns a `long` identifier for the notification. Pass this to `HideAsync`
to remove it programmatically before the user interacts with it.

```csharp
long id = await service.ShowAsync(request);

// Remove it after two seconds
await Task.Delay(TimeSpan.FromSeconds(2));
await service.HideAsync(id);
```

---

## Callback threading

Callbacks are fired on a background thread:

- **Windows** — callbacks arrive on a WinRT thread-pool thread.
- **Linux** — callbacks arrive on the GLib main loop thread.
- **macOS** — callbacks arrive on a background GCD thread managed by
  `UNUserNotificationCenter`.

If you need to update UI elements from a callback, marshal the call to your UI thread
(e.g. `Dispatcher.InvokeAsync` on WPF, `Control.Invoke` on WinForms, or
`MainThread.BeginInvokeOnMainThread` on MAUI).

---

## INotificationService interface

```csharp
public interface INotificationService : IDisposable
{
    // False if the platform is unsupported or initialisation failed.
    bool IsSupported { get; }

    // Shows a notification. Returns the notification ID on success.
    // Throws PlatformNotSupportedException if IsSupported is false.
    Task<long> ShowAsync(NotificationRequest request,
        CancellationToken cancellationToken = default);

    // Removes the notification from the notification centre.
    Task HideAsync(long notificationId,
        CancellationToken cancellationToken = default);
}
```

Dispose the service when your application exits to release the native resources
(WinToastLib STA thread on Windows, GLib main loop on Linux, UNUserNotificationCenter
cleanup on macOS).

---

## Taskbar progress

`ITaskbarProgressService` drives the progress indicator on the application's taskbar button
(Windows), launcher entry (Linux) or Dock tile (macOS) — the same green/red bar Windows
Explorer shows during a file copy. Use it to surface the progress of a long-running
operation without a custom UI.

| Platform | Backend | Requirement |
|----------|---------|-------------|
| Windows  | `ITaskbarList3` | A top-level window handle (defaults to the console window) |
| Linux    | Unity LauncherEntry D-Bus API (KDE Plasma, Unity, Dash-to-Dock, Plank, Latte) | A `.desktop` file whose id is supplied via `DesktopFileId` |
| macOS    | `NSProgressIndicator` drawn on the Dock tile | A bundled GUI app that owns a Dock tile |

If the indicator is unavailable on the current platform, `IsSupported` is `false` and all
methods are silent no-ops.

### Creating the service

```csharp
// Direct (no DI container)
using var progress = ServiceCollectionExtensions.CreateTaskbarProgressService(opts =>
{
    opts.DesktopFileId = "com.example.MyApp";  // Linux: the app's .desktop file id
});

// With Microsoft.Extensions.DependencyInjection
services.AddTaskbarProgress(opts =>
{
    opts.DesktopFileId = "com.example.MyApp";
});
```

### Reporting progress

```csharp
if (!progress.IsSupported)
    return;

// Determinate progress, by fraction (0.0–1.0, clamped)…
progress.SetProgress(0.25);

// …or by completed / total counts.
for (ulong i = 0; i <= total; i++)
{
    DoWork(i);
    progress.SetProgress(i, total);   // total must be greater than zero
}

// Clear the indicator when finished.
progress.SetState(TaskbarProgressState.None);
```

Calling either `SetProgress` overload switches the indicator to the `Normal` state, unless
it is currently in the `Error` or `Paused` state (those are preserved so a paused/failed
operation keeps its colour while its value updates).

### States

```csharp
progress.SetState(TaskbarProgressState.Indeterminate); // work of unknown length
progress.SetState(TaskbarProgressState.Paused);        // operation paused
progress.SetState(TaskbarProgressState.Error);         // operation failed
progress.SetState(TaskbarProgressState.None);          // clear the indicator
```

| State | Windows | Linux | macOS |
|-------|---------|-------|-------|
| `None` | No bar | No bar | No bar |
| `Indeterminate` | Pulsing marquee bar | Falls back to a 0% bar | Animated bar |
| `Normal` | Green bar | Bar at the current value | Bar at the current value |
| `Paused` | Yellow bar | Same as `Normal` | Same as `Normal` |
| `Error` | Red bar | Launcher entry flagged "urgent" | Same as `Normal` |

### Targeting a window (Windows)

By default the Windows backend targets the console window (`GetConsoleWindow()`). For a
WPF/WinForms app, point it at your main window's HWND so the bar appears on the right
taskbar button. This is a no-op on Linux and macOS.

```csharp
// WPF
var hwnd = new System.Windows.Interop.WindowInteropHelper(mainWindow).Handle;
progress.SetWindow(hwnd);

// WinForms
progress.SetWindow(form.Handle);

// Revert to the console window
progress.SetWindow(IntPtr.Zero);
```

### ITaskbarProgressService interface

```csharp
public interface ITaskbarProgressService : IDisposable
{
    // False if a progress indicator is unavailable on this platform.
    bool IsSupported { get; }

    // Sets the visual state without changing the value (None clears it).
    void SetState(TaskbarProgressState state);

    // Sets the value and switches to Normal (Error/Paused are preserved).
    void SetProgress(ulong completed, ulong total);   // total must be > 0
    void SetProgress(double fraction);                // 0.0–1.0, clamped

    // Windows only: target a specific top-level window (Zero reverts to the console window).
    void SetWindow(IntPtr windowHandle);
}
```

---

## Jump lists

A *jump list* is the menu of quick action shortcuts attached to an application's taskbar
button (Windows), launcher icon (Linux) or Dock icon (macOS). Notify.NET exposes this
through `IJumpListService`, which presents a single, uniform live-callback API across all
three platforms: when the user clicks a task, your already-running process receives an
`IJumpListHandler.OnTaskActivated(taskId)` call.

| Platform | Backend | Activation model |
|----------|---------|------------------|
| Windows  | Shell `ICustomDestinationList` "user tasks" (Windows 7+) | Relaunch + single-instance forwarding |
| Linux    | freedesktop.org Desktop Actions in the app's `.desktop` file (GNOME, KDE, Unity, …) | Relaunch + single-instance forwarding |
| macOS    | Dock menu via the application delegate (bundled GUI app only) | Live, in-process — no relaunch |

On Windows and Linux a clicked task fundamentally relaunches the executable with a hidden
`--notify-jumplist <id>` argument. Notify.NET bundles a single-instance channel (a named
mutex plus a named pipe) that forwards the id to the running primary instance, so the
handler always fires live — uniform with macOS's natively-live Dock menu.

Nothing is registered and no mutex, pipe or OS entry is created until you call `SetTasks`
or `SetHandler`, so applications that do not use jump lists incur zero overhead.

### Creating the service

```csharp
// Direct (no DI container)
using var jumpList = ServiceCollectionExtensions.CreateJumpListService(opts =>
{
    opts.AppName        = "My App";
    opts.AppUserModelId = "MyCompany.MyApp";  // Windows: must match the notification AUMI
    opts.DesktopFileId  = "com.example.MyApp"; // Linux: the app's .desktop file id
});

// With Microsoft.Extensions.DependencyInjection
services.AddJumpList(opts =>
{
    opts.AppName        = "My App";
    opts.AppUserModelId = "MyCompany.MyApp";
    opts.DesktopFileId  = "com.example.MyApp";
});
```

`CreateJumpListService` / `AddJumpList` select the correct backend for the current OS.
On unsupported platforms they return a no-op service where `IsSupported` is `false`.

### Wiring up activation

On Windows and Linux, call `TryHandleActivation` at the very top of `Main`, before any UI
is shown. If this launch is a forwarded jump-list click, it returns `true` and the process
should exit immediately. Then attach a handler and register the tasks — the first call to
`SetTasks` / `SetHandler` makes this process the primary instance and starts the listener.

```csharp
public static int Main(string[] args)
{
    using var jumpList = ServiceCollectionExtensions.CreateJumpListService(opts =>
    {
        opts.AppName        = "My App";
        opts.AppUserModelId = "MyCompany.MyApp";
        opts.DesktopFileId  = "com.example.MyApp";
    });

    // Forward a jump-list click to the already-running instance, then exit.
    if (jumpList.TryHandleActivation(args))
        return 0;

    jumpList.SetHandler(new MyJumpListHandler());
    jumpList.SetTasks(new[]
    {
        new JumpListTask("new-doc",  "New Document"),
        new JumpListTask("open-last","Open Last File", description: "Reopen the most recent file"),
        new JumpListTask("settings", "Settings", iconPath: @"C:\Apps\MyApp\settings.ico"),
    });

    RunApplication();   // your normal startup / message loop
    return 0;
}

public sealed class MyJumpListHandler : IJumpListHandler
{
    public void OnTaskActivated(string taskId)
    {
        // Fired on a background thread — marshal to your UI thread before touching UI.
        switch (taskId)
        {
            case "new-doc":   CreateDocument(); break;
            case "open-last": OpenLastFile();   break;
            case "settings":  ShowSettings();   break;
        }
    }
}
```

If the app was launched cold by a jump-list click (no primary instance was running), the
activation is captured and replayed to the handler once one is set.

### JumpListTask

```csharp
new JumpListTask(
    id:          "open-last",          // stable id passed back to OnTaskActivated (no whitespace)
    title:       "Open Last File",     // label shown in the menu
    description: "Reopen the most recent file", // tooltip (Windows); optional
    iconPath:    @"C:\Apps\MyApp\recent.ico",   // optional; defaults to the host exe icon
    iconIndex:   0);                   // icon index within iconPath (Windows)
```

### Managing tasks

```csharp
jumpList.SetTasks(tasks);   // replace the current task set (empty sequence == ClearTasks)
jumpList.ClearTasks();      // remove all tasks registered by this app
jumpList.SetHandler(null);  // detach the handler
```

### Options

| Option | Purpose |
|--------|---------|
| `AppName` | Human-readable name; used if a minimal Linux `.desktop` file must be created. |
| `AppUserModelId` | Windows — must match the AUMI used for notifications so the list attaches to the right taskbar button. |
| `DesktopFileId` | Linux — the app's `.desktop` file id (with or without the `.desktop` suffix). Defaults to the process name. |
| `ExecutablePath` | Windows/Linux — absolute path to relaunch on click. When null, the current process executable is used; pass an explicit path for framework-dependent `dotnet` apps where the auto-detected path may be the shared host. Ignored on macOS. |

### IJumpListService interface

```csharp
public interface IJumpListService : IDisposable
{
    // False if jump lists are unavailable on this platform; all methods become no-ops.
    bool IsSupported { get; }

    // Registers the handler for OnTaskActivated events (also starts the listener).
    void SetHandler(IJumpListHandler? handler);

    // Replaces the application's jump-list tasks (empty sequence clears them).
    void SetTasks(IEnumerable<JumpListTask> tasks);

    // Removes all tasks registered by this application.
    void ClearTasks();

    // Call once at the start of Main. Returns true if the launch was a forwarded
    // activation and the caller should exit immediately.
    bool TryHandleActivation(string[] args);
}
```

---

## Platform notes

### Windows

- Requires Windows 8 or later. `IsSupported` returns `false` on earlier versions.
- The `AppUserModelId` must match an application shortcut in the Start Menu. The native
  wrapper creates this shortcut automatically on first run when it does not already exist,
  but the shortcut creation requires that the process can write to the user's
  `%APPDATA%\Microsoft\Windows\Start Menu\Programs` folder.
- `WinToastWrapper.dll` is loaded at runtime from `runtimes/win-<arch>/native/` relative
  to the entry assembly. For published single-file applications, ensure the DLL is
  published alongside the executable.
- Toast callbacks are delivered on a WinRT thread-pool thread, not the STA thread. The
  library handles this internally.
- Jump lists use the shell `ICustomDestinationList` "user tasks" API (Windows 7+) — pure
  managed COM interop, no native DLL required. The jump list attaches to the taskbar button
  matching `AppUserModelId`, so it must be the same id used for notifications. The COM work
  runs on a dedicated STA thread the library creates lazily on first use.
- Taskbar progress uses `ITaskbarList3` and needs a top-level window handle. It defaults to
  the console window (`GetConsoleWindow()`); call `SetWindow` with your WPF/WinForms main
  window HWND to move the bar onto that taskbar button. The COM work runs on its own lazily
  created STA thread.

### Linux

`libnotify` must be installed at runtime. Install it via your package manager:

```
# Debian / Ubuntu
sudo apt install libnotify4

# Fedora / RHEL
sudo dnf install libnotify

# Arch
sudo pacman -S libnotify
```

A running D-Bus notification daemon is required (GNOME Shell, KDE Plasma, and most other
desktop environments provide one). Notifications will be silently dropped if no daemon is
running. `IsSupported` reflects whether `notify_init()` succeeded, not whether a daemon
is present.

Image support via `gdk-pixbuf` requires `libgdk-pixbuf-2.0` to be installed, which is
typically included as a dependency of `libnotify4`.

Taskbar progress uses the Unity LauncherEntry D-Bus API, honoured by KDE Plasma, Unity,
Dash-to-Dock, Plank and Latte. It requires the app to ship (or have created) a `.desktop`
file whose id is supplied via `DesktopFileId`; the launcher matches the entry by that id.
Desktop environments without LauncherEntry support simply show no bar.

Jump lists are written as `Actions` into the application's `.desktop` file. If no installed
`.desktop` file is found for `DesktopFileId`, a minimal one is created under
`$XDG_DATA_HOME/applications` (default `~/.local/share/applications`). Writing the file is
best-effort — a read-only or absent home directory will not crash the application. Each
action's `Exec` relaunches the executable with the activation argument, which the bundled
single-instance layer forwards to the running primary instance.

### macOS

- Requires macOS 10.14 (Mojave) or later.
- On first use, macOS presents an authorisation dialog asking the user to allow
  notifications. `MNW_Initialize` (called from the `MacOSNotificationService` constructor)
  blocks until the user responds. `IsSupported` is `false` if permission was denied.
- The user can revoke permission at any time in System Settings > Notifications. Subsequent
  calls to `ShowAsync` will fail silently (the OS will not display the notification and no
  callback fires) until permission is re-granted.
- Action button callbacks and dismiss callbacks require the process to remain running after
  the notification is shown, because `UNUserNotificationCenterDelegate` delivers responses
  to the live process. If the process exits before the user interacts, the callbacks are
  never fired.
- Non-bundled processes (bare `dotnet` CLI applications) can display banners but may not
  always receive action callbacks on all OS versions. Wrap the application as an `.app`
  bundle or sign it with an appropriate entitlement for reliable callback delivery in
  production.
- The notification body-tap and button-tap events on macOS are terminal events — the
  `OnDismissed` callback is not fired after the user activates a notification or clicks
  a button (unlike Windows, where WinToastLib always fires the dismissed event after any
  interaction).
- Taskbar progress draws an `NSProgressIndicator` along the bottom of the **Dock tile**.
  This is only visible for a regular bundled GUI application that owns a Dock tile and has a
  running main loop; a bare console process has none, so the calls are harmless no-ops. The
  Dock cannot tint the bar, so `Paused` and `Error` render the same as `Normal`.
- Jump-list tasks appear in the **Dock menu** (right-click / click-and-hold of the Dock
  icon) and fire `OnTaskActivated` live in-process — there is no relaunch, so
  `TryHandleActivation` always returns `false` on macOS. This is only effective for a
  regular bundled GUI application with a running main loop; a bare console process has no
  Dock menu and the calls are harmless no-ops. The wrapper supplies the menu via the
  application delegate's `applicationDockMenu:`, installing its own delegate if the app has
  none, or adding the method to the existing delegate without clobbering a Dock menu the app
  already provides.

---

## Building the native libraries from source

Pre-built native binaries are included in the NuGet package. You only need to build from
source if you are making changes to the native wrapper code.

### Windows (WinToastWrapper.dll)

Requires Visual Studio 2022 with the "Desktop development with C++" workload.

```
msbuild native\WinToastWrapper\WinToastWrapper.vcxproj /p:Configuration=Release /p:Platform=x64
msbuild native\WinToastWrapper\WinToastWrapper.vcxproj /p:Configuration=Release /p:Platform=Win32
msbuild native\WinToastWrapper\WinToastWrapper.vcxproj /p:Configuration=Release /p:Platform=ARM64
```

Output is written to `runtimes\win-<arch>\native\WinToastWrapper.dll`.

### macOS (libMacNotifyWrapper.dylib)

Requires Xcode command-line tools (`xcode-select --install`).

```
make -C native/MacNotifyWrapper install
```

This cross-compiles both an `arm64` (Apple Silicon, macOS 11+) and an `x86_64` (Intel,
macOS 10.14+) slice and copies them to `runtimes/osx-arm64/native/` and
`runtimes/osx-x64/native/` respectively.

To also produce a universal binary:

```
make -C native/MacNotifyWrapper universal
```

---

## License

MIT. See [LICENSE](LICENSE) for details.
WinToastLib is copyright (c) mohabouje, also distributed under the MIT License.
