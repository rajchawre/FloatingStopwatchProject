using System;
using System.Windows.Threading;

namespace FloatingStopwatch
{
    /// <summary>
    /// Keeps a window at the very top of the topmost z-order band.
    /// PowerPoint's slideshow window is itself topmost, and among topmost windows the one raised
    /// most recently wins. So we re-raise (without activating) whenever the foreground window
    /// changes and on a light timer. SWP_NOACTIVATE means this never steals focus.
    /// </summary>
    internal sealed class TopmostGuard : IDisposable
    {
        private readonly IntPtr _hwnd;
        private readonly DispatcherTimer _timer;
        private readonly NativeMethods.WinEventDelegate _callback; // field keeps the delegate alive
        private IntPtr _hook = IntPtr.Zero;
        private bool _enabled;

        /// <summary>Return false to skip raising (e.g. window hidden or minimized).</summary>
        public Func<bool> CanAssert { get; set; }

        public TopmostGuard(IntPtr hwnd)
        {
            _hwnd = hwnd;
            _callback = OnWinEvent;
            _timer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _timer.Tick += (s, e) => AssertNow();
        }

        public bool Enabled
        {
            get { return _enabled; }
            set
            {
                if (value == _enabled) return;
                _enabled = value;
                if (value)
                {
                    _hook = NativeMethods.SetWinEventHook(
                        NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND,
                        IntPtr.Zero, _callback, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);
                    _timer.Start();
                    AssertNow();
                }
                else
                {
                    _timer.Stop();
                    if (_hook != IntPtr.Zero)
                    {
                        NativeMethods.UnhookWinEvent(_hook);
                        _hook = IntPtr.Zero;
                    }
                }
            }
        }

        public void AssertNow()
        {
            if (!_enabled) return;
            if (CanAssert != null && !CanAssert()) return;
            NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        }

        private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            AssertNow();
        }

        public void Dispose()
        {
            Enabled = false;
        }
    }
}
