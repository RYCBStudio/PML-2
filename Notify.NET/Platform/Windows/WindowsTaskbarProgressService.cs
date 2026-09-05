using System;
using System.Collections.Concurrent;
using System.Threading;
using Notify.NET.Abstractions;

namespace Notify.NET.Platform.Windows
{
    /// <summary>
    /// <see cref="ITaskbarProgressService"/> implementation backed by the shell <c>ITaskbarList3</c>
    /// COM interface. No native wrapper DLL is required.
    ///
    /// Two mechanisms are driven together so that progress is visible across host environments:
    /// <list type="bullet">
    ///   <item><description>
    ///     <b><c>ITaskbarList3</c></b> — sets progress on the taskbar button of the target window.
    ///     This is the mechanism for GUI apps (pass the window via <see cref="SetWindow"/>) and for
    ///     the classic console host (<c>conhost.exe</c>), whose console window owns a taskbar button.
    ///   </description></item>
    ///   <item><description>
    ///     <b>OSC 9;4</b> — the ConEmu/Windows-Terminal progress escape sequence, written to stdout.
    ///     Under Windows Terminal (the Windows 11 default) the app runs through a ConPTY and
    ///     <c>GetConsoleWindow()</c> returns a hidden proxy window with no taskbar button, so
    ///     <c>ITaskbarList3</c> has no visible effect; Windows Terminal instead reflects this
    ///     sequence on its own taskbar button. It is emitted only when targeting the default console
    ///     window and stdout is an interactive console (never when redirected, to avoid corrupting
    ///     piped output).
    ///   </description></item>
    /// </list>
    ///
    /// Threading model:
    ///   <c>ITaskbarList3</c> is an apartment-threaded in-proc COM object. This service owns a
    ///   dedicated STA background thread; the COM object is created on it and every call is
    ///   marshalled onto that thread via a work-item queue, mirroring
    ///   <see cref="WindowsNotificationService"/>.
    /// </summary>
    public sealed class WindowsTaskbarProgressService : ITaskbarProgressService
    {
        private readonly Thread _staThread;
        private readonly BlockingCollection<Action> _workQueue = new BlockingCollection<Action>();
        private readonly ManualResetEventSlim _initialised = new ManualResetEventSlim(false);

        // OSC 9;4 is only meaningful for the console scenario and must not pollute redirected output.
        private readonly bool _consoleEligible = !Console.IsOutputRedirected;

        private TaskbarListNative.ITaskbarList3? _taskbarList;
        private IntPtr _hwnd;
        private TaskbarProgressState _state = TaskbarProgressState.None;
        private int _percent;
        private bool _explicitWindow;
        private volatile bool _isSupported;
        private volatile bool _disposed;

        /// <inheritdoc/>
        public bool IsSupported => _isSupported;

        public WindowsTaskbarProgressService()
        {
            _hwnd = TaskbarListNative.GetConsoleWindow();

            _staThread = new Thread(StaThreadProc)
            {
                Name = "Notify.NET Taskbar STA",
                IsBackground = true
            };
            _staThread.SetApartmentState(ApartmentState.STA);
            _staThread.Start();

            _initialised.Wait();
        }

        // ------------------------------------------------------------------
        // ITaskbarProgressService
        // ------------------------------------------------------------------

        /// <inheritdoc/>
        public void SetWindow(IntPtr windowHandle)
        {
            if (_disposed || !_isSupported) return;
            EnqueueOnSta(() =>
            {
                if (windowHandle != IntPtr.Zero)
                {
                    _hwnd = windowHandle;
                    // Targeting a real GUI window: stop emitting console sequences and clear any
                    // progress already shown on the terminal's taskbar button.
                    if (_explicitWindow == false) EmitConsole(TaskbarProgressState.None, 0);
                    _explicitWindow = true;
                }
                else
                {
                    _hwnd = TaskbarListNative.GetConsoleWindow();
                    _explicitWindow = false;
                }
            });
        }

        /// <inheritdoc/>
        public void SetState(TaskbarProgressState state)
        {
            if (_disposed || !_isSupported) return;
            EnqueueOnSta(() =>
            {
                _state = state;
                if (state == TaskbarProgressState.None) _percent = 0;
                _taskbarList!.SetProgressState(_hwnd, MapState(state));
                EmitConsole(_state, _percent);
            });
        }

        /// <inheritdoc/>
        public void SetProgress(ulong completed, ulong total)
        {
            if (_disposed || !_isSupported) return;
            if (total == 0) throw new ArgumentOutOfRangeException(nameof(total), "Total must be greater than zero.");

            EnqueueOnSta(() =>
            {
                // SetProgressValue implicitly switches NoProgress/Indeterminate to Normal.
                // Preserve an explicit Error/Paused colour if one is currently set.
                if (_state != TaskbarProgressState.Error && _state != TaskbarProgressState.Paused)
                {
                    _state = TaskbarProgressState.Normal;
                    _taskbarList!.SetProgressState(_hwnd, TaskbarListNative.TBPFLAG.TBPF_NORMAL);
                }
                _taskbarList!.SetProgressValue(_hwnd, completed, total);

                _percent = (int)Math.Round((double)completed / total * 100.0);
                EmitConsole(_state, _percent);
            });
        }

        /// <inheritdoc/>
        public void SetProgress(double fraction)
        {
            double clamped = fraction < 0 ? 0 : (fraction > 1 ? 1 : fraction);
            SetProgress((ulong)Math.Round(clamped * 1000.0), 1000UL);
        }

        // ------------------------------------------------------------------
        // IDisposable
        // ------------------------------------------------------------------

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _workQueue.CompleteAdding();
            if (_staThread.IsAlive)
                _staThread.Join(TimeSpan.FromSeconds(5));

            _workQueue.Dispose();
            _initialised.Dispose();
        }

        // ------------------------------------------------------------------
        // Private helpers
        // ------------------------------------------------------------------

        private void EnqueueOnSta(Action action)
        {
            try { _workQueue.Add(action); }
            catch (InvalidOperationException) { /* queue completed — service disposed */ }
        }

        private static TaskbarListNative.TBPFLAG MapState(TaskbarProgressState state)
        {
            switch (state)
            {
                case TaskbarProgressState.Indeterminate: return TaskbarListNative.TBPFLAG.TBPF_INDETERMINATE;
                case TaskbarProgressState.Normal:        return TaskbarListNative.TBPFLAG.TBPF_NORMAL;
                case TaskbarProgressState.Paused:        return TaskbarListNative.TBPFLAG.TBPF_PAUSED;
                case TaskbarProgressState.Error:         return TaskbarListNative.TBPFLAG.TBPF_ERROR;
                default:                                 return TaskbarListNative.TBPFLAG.TBPF_NOPROGRESS;
            }
        }

        /// <summary>
        /// Writes the ConEmu/Windows-Terminal OSC 9;4 progress sequence to stdout so the terminal's
        /// own taskbar button reflects progress (the only mechanism that works under ConPTY). Emitted
        /// only when targeting the default console window and stdout is a real interactive console.
        /// </summary>
        private void EmitConsole(TaskbarProgressState state, int percent)
        {
            if (_explicitWindow || !_consoleEligible) return;

            // OSC 9;4 state codes: 0=remove, 1=normal, 2=error, 3=indeterminate, 4=warning(paused).
            int code;
            switch (state)
            {
                case TaskbarProgressState.Indeterminate: code = 3; break;
                case TaskbarProgressState.Error:         code = 2; break;
                case TaskbarProgressState.Paused:        code = 4; break;
                case TaskbarProgressState.None:          code = 0; break;
                default:                                 code = 1; break; // Normal
            }

            int clamped = percent < 0 ? 0 : (percent > 100 ? 100 : percent);

            try
            {
                Console.Out.Write("\x1b]9;4;" + code + ";" + clamped + "\x07");
                Console.Out.Flush();
            }
            catch { /* no console attached — ignore */ }
        }

        private void StaThreadProc()
        {
            try
            {
                var instance = (TaskbarListNative.ITaskbarList3)new TaskbarListNative.TaskbarInstance();
                instance.HrInit();
                _taskbarList = instance;
                _isSupported = true;
            }
            catch (Exception)
            {
                // COM object unavailable (pre-Win7 or restricted) — degrade to no-op.
                _isSupported = false;
            }
            finally
            {
                _initialised.Set();
            }

            if (!_isSupported) return;

            try
            {
                foreach (Action work in _workQueue.GetConsumingEnumerable())
                    work();
            }
            catch (InvalidOperationException) { /* queue completed */ }
            finally
            {
                if (_taskbarList != null)
                {
                    // Clear any visible progress before releasing the COM object.
                    try { _taskbarList.SetProgressState(_hwnd, TaskbarListNative.TBPFLAG.TBPF_NOPROGRESS); }
                    catch { /* best effort */ }
                    EmitConsole(TaskbarProgressState.None, 0);

                    System.Runtime.InteropServices.Marshal.FinalReleaseComObject(_taskbarList);
                    _taskbarList = null;
                }
            }
        }
    }
}
