using System;
using System.Diagnostics;

namespace FloatingStopwatch
{
    /// <summary>
    /// Stopwatch backed by System.Diagnostics.Stopwatch (QueryPerformanceCounter): high-resolution,
    /// monotonic, and independent of window focus or UI timer ticks.
    /// </summary>
    public sealed class StopwatchEngine
    {
        private readonly Stopwatch _sw = new Stopwatch();
        private TimeSpan _offset = TimeSpan.Zero; // time restored from a previous run

        public bool IsRunning { get { return _sw.IsRunning; } }
        public TimeSpan Elapsed { get { return _offset + _sw.Elapsed; } }

        public void Start() { _sw.Start(); }
        public void Stop() { _sw.Stop(); }

        public void Reset()
        {
            _sw.Reset();
            _offset = TimeSpan.Zero;
        }

        /// <summary>Load a saved elapsed time; the clock is left paused.</summary>
        public void Restore(TimeSpan elapsed)
        {
            _sw.Reset();
            _offset = elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
        }
    }

    public sealed class CountdownEngine
    {
        private readonly Stopwatch _sw = new Stopwatch();

        public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(5);

        public bool IsRunning { get { return _sw.IsRunning; } }
        public bool HasProgress { get { return _sw.Elapsed > TimeSpan.Zero; } }

        public TimeSpan Remaining
        {
            get
            {
                TimeSpan r = Duration - _sw.Elapsed;
                return r < TimeSpan.Zero ? TimeSpan.Zero : r;
            }
        }

        public bool IsFinished
        {
            get { return Duration > TimeSpan.Zero && Remaining <= TimeSpan.Zero; }
        }

        public void Start()
        {
            if (Remaining > TimeSpan.Zero) _sw.Start();
        }

        public void Stop() { _sw.Stop(); }
        public void Reset() { _sw.Reset(); }
    }
}
