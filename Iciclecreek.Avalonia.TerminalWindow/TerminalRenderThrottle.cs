using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Iciclecreek.TerminalWindow
{

    /// <summary>
    /// Synchronizes control invalidation to a target frame rate, so all terminals get invalidated together.
    /// </summary>
    public static class TerminalRenderThrottle
    {
        // Target frame rate (30 FPS = 33 ms)
        private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(33);

        // Controls waiting to be invalidated
        private static readonly HashSet<Control> Pending = new();

        // State
        private static bool _frameScheduled;
        private static DateTime _lastFrame = DateTime.MinValue;
        private static volatile bool _isPaused;

        /// <summary>
        /// Pauses all terminal rendering (e.g. while the host window is minimized).
        /// Invalidate requests are coalesced but not flushed until <see cref="Resume"/> is called,
        /// so restoring the window repaints every terminal exactly once with the latest content.
        /// </summary>
        public static bool IsPaused
        {
            get => _isPaused;
            private set => _isPaused = value;
        }

        /// <summary>
        /// Suspend flushing. Pending and future invalidate requests are coalesced into the
        /// pending set instead of reaching the render loop.
        /// </summary>
        public static void Pause() => _isPaused = true;

        /// <summary>
        /// Resume flushing and immediately repaint every terminal that changed while paused.
        /// </summary>
        public static void Resume()
        {
            _isPaused = false;
            if (_frameScheduled)
            {
                return;
            }

            lock (Pending)
            {
                if (Pending.Count == 0)
                {
                    return;
                }
            }

            _frameScheduled = true;
            Dispatcher.UIThread.Post(Flush);
        }

        /// <summary>
        /// Request that a control be invalidated on the next coordinated frame.
        /// </summary>
        public static void RequestInvalidate(this Control control)
        {
            if (control == null)
                return;

            lock (Pending)
                Pending.Add(control);

            // 暂停期间（如窗口最小化）只登记、不调度，避免不可见终端持续触发渲染帧。
            if (_isPaused)
                return;

            if (!_frameScheduled)
            {
                _frameScheduled = true;
                ScheduleFrame();
            }
        }

        private static void ScheduleFrame()
        {
            var now = DateTime.UtcNow;
            var elapsed = now - _lastFrame;

            // If enough time has passed, flush immediately on the UI thread
            if (elapsed >= FrameInterval)
            {
                Dispatcher.UIThread.Post(Flush);
                return;
            }

            // Otherwise schedule a delayed flush
            var delay = FrameInterval - elapsed;

            Dispatcher.UIThread.Post(async () =>
            {
                await Task.Delay(delay);
                Flush();
            });
        }

        private static void Flush()
        {
            _frameScheduled = false;
            _lastFrame = DateTime.UtcNow;

            // 暂停期间延迟到达的 Flush 直接丢弃，待 Resume 时统一重绘。
            if (_isPaused)
                return;

            lock (Pending)
            {
                if (Pending.Count == 0)
                    return;

                foreach (var control in Pending)
                    control.InvalidateVisual();

                Pending.Clear();
            }
        }
    }
}
