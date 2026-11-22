using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json.Serialization;

namespace FloatingStopwatch
{
    public sealed class LapEntry : INotifyPropertyChanged
    {
        private int _number;
        private string _name = "";

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise(string property)
        {
            PropertyChangedEventHandler h = PropertyChanged;
            if (h != null) h(this, new PropertyChangedEventArgs(property));
        }

        /// <summary>Serial number, re-assigned automatically when entries are deleted.</summary>
        public int Number
        {
            get { return _number; }
            set { if (_number != value) { _number = value; Raise("Number"); } }
        }

        public int Session { get; set; }

        /// <summary>Editable activity / lap name.</summary>
        public string Name
        {
            get { return _name; }
            set { if (_name != value) { _name = value ?? ""; Raise("Name"); } }
        }

        /// <summary>Duration of this lap (time since the previous lap).</summary>
        public TimeSpan LapTime { get; set; }

        /// <summary>Total elapsed time on the stopwatch when LAP was pressed.</summary>
        public TimeSpan TotalTime { get; set; }

        [JsonIgnore]
        public string LapText { get { return TimeFormat.Lap(LapTime); } }

        [JsonIgnore]
        public string TotalText { get { return TimeFormat.Lap(TotalTime); } }
    }

    public sealed class AppSettings
    {
        // Window
        public double? Left { get; set; }
        public double? Top { get; set; }
        public double Scale { get; set; } = 1.0;
        public bool Compact { get; set; } = false;
        public bool LockPosition { get; set; } = false;
        public bool AlwaysOnTop { get; set; } = true;
        public bool TakeFocusOnClick { get; set; } = false;
        public bool GlobalHotkeys { get; set; } = true;

        // Look
        public double TimerFontSize { get; set; } = 56;
        public string TextColor { get; set; } = "#FFFFFF";
        public string BackgroundColor { get; set; } = "#1E1E1E";
        public double BackgroundOpacity { get; set; } = 0.85;
        public bool ShowButtons { get; set; } = true;

        // Sounds
        public bool SoundsEnabled { get; set; } = true;
        public bool SoundOnStartStop { get; set; } = true;
        public bool SoundOnCountdownEnd { get; set; } = true;

        // Countdown
        public bool CountdownMode { get; set; } = false;
        public int CountdownMinutes { get; set; } = 5;
        public int CountdownSeconds { get; set; } = 0;
    }

    public sealed class SessionData
    {
        public int SessionNumber { get; set; } = 1;
        public long ElapsedTicks { get; set; }
        public long LastLapTotalTicks { get; set; }
        public List<LapEntry> Laps { get; set; } = new List<LapEntry>();
    }

    internal static class TimeFormat
    {
        /// <summary>HH:MM:SS for the main display.</summary>
        public static string Main(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}",
                (int)t.TotalHours, t.Minutes, t.Seconds);
        }

        /// <summary>MM:SS.cc (or H:MM:SS.cc past one hour) for the lap list and CSV.</summary>
        public static string Lap(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            int hours = (int)t.TotalHours;
            int centis = t.Milliseconds / 10;
            if (hours > 0)
                return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}.{3:00}",
                    hours, t.Minutes, t.Seconds, centis);
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}.{2:00}",
                t.Minutes, t.Seconds, centis);
        }
    }
}
