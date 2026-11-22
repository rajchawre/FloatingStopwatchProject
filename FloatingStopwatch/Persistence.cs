using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FloatingStopwatch
{
    /// <summary>Settings, session and lap list, persisted under %AppData%\FloatingStopwatch.</summary>
    internal static class AppState
    {
        private static readonly string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FloatingStopwatch");
        private static readonly string SettingsPath = Path.Combine(Dir, "settings.json");
        private static readonly string SessionPath = Path.Combine(Dir, "session.json");
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };

        public static AppSettings Settings { get; private set; } = new AppSettings();
        public static SessionData Session { get; private set; } = new SessionData();
        public static ObservableCollection<LapEntry> Laps { get; private set; } = new ObservableCollection<LapEntry>();

        public static void Load()
        {
            try { Directory.CreateDirectory(Dir); } catch { }
            Settings = Read<AppSettings>(SettingsPath) ?? new AppSettings();
            Session = Read<SessionData>(SessionPath) ?? new SessionData();
            if (Session.SessionNumber < 1) Session.SessionNumber = 1;
            Laps = new ObservableCollection<LapEntry>(Session.Laps ?? new List<LapEntry>());
            Renumber();
        }

        public static void SaveSettings()
        {
            Write(SettingsPath, Settings);
        }

        public static void SaveSession()
        {
            Session.Laps = Laps.ToList();
            Write(SessionPath, Session);
        }

        /// <summary>Re-assign serial numbers 1..N after any deletion.</summary>
        public static void Renumber()
        {
            for (int i = 0; i < Laps.Count; i++) Laps[i].Number = i + 1;
        }

        public static void RemoveLaps(IEnumerable<LapEntry> entries)
        {
            foreach (LapEntry entry in entries.ToList()) Laps.Remove(entry);
            Renumber();
            SaveSession();
        }

        public static void ClearLaps()
        {
            Laps.Clear();
            SaveSession();
        }

        private static T Read<T>(string path) where T : class
        {
            try
            {
                if (!File.Exists(path)) return null;
                return JsonSerializer.Deserialize<T>(File.ReadAllText(path));
            }
            catch
            {
                return null; // corrupt file: fall back to defaults
            }
        }

        private static void Write<T>(string path, T value)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(value, JsonOptions));
                File.Move(tmp, path, true); // atomic-ish replace so a crash never leaves a half-written file
            }
            catch
            {
                // Saving is best-effort; never crash the stopwatch because of disk problems.
            }
        }
    }
}
