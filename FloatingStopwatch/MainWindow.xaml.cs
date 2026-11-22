using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace FloatingStopwatch
{
    public partial class MainWindow : Window
    {
        private const int HkStartStop = 1;
        private const int HkLap = 2;
        private const int HkReset = 3;
        private const int HkHide = 4;

        private readonly StopwatchEngine _sw = new StopwatchEngine();
        private readonly CountdownEngine _cd = new CountdownEngine();
        private readonly DispatcherTimer _ui;        // refreshes the display, runs only while a clock is running
        private readonly DispatcherTimer _autosave;  // persists the session every 15 s while running
        private readonly DispatcherTimer _settingsSave; // debounces settings writes while a slider is dragged

        private IntPtr _hwnd = IntPtr.Zero;
        private HwndSource _source;
        private TopmostGuard _guard;
        private LapsWindow _lapsWindow;
        private SettingsWindow _settingsWindow;

        private static AppSettings S { get { return AppState.Settings; } }
        private static SessionData D { get { return AppState.Session; } }
        private bool CountdownMode { get { return S.CountdownMode; } }
        private bool IsRunning { get { return CountdownMode ? _cd.IsRunning : _sw.IsRunning; } }

        public MainWindow()
        {
            InitializeComponent();

            _sw.Restore(TimeSpan.FromTicks(D.ElapsedTicks)); // restore previous session (paused)
            ReloadCountdownDuration();

            _ui = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(100) };
            _ui.Tick += (s, e) => UpdateDisplay();
            _autosave = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(15) };
            _autosave.Tick += (s, e) => SaveSession();
            _settingsSave = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(500) };
            _settingsSave.Tick += (s, e) => { _settingsSave.Stop(); AppState.SaveSettings(); };

            // Keep the "SESSION n . N LAPS" label current when laps are added, deleted or cleared.
            AppState.Laps.CollectionChanged += (s, e) => RefreshAll();

            RestorePosition();
            ApplyVisuals();
            UpdateDisplay();
            RefreshAll();
        }

        // ------------------------------------------------------------------ native window setup

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _hwnd = new WindowInteropHelper(this).Handle;
            _source = HwndSource.FromHwnd(_hwnd);
            _source.AddHook(WndProc);
            _guard = new TopmostGuard(_hwnd)
            {
                CanAssert = () => IsVisible && WindowState != WindowState.Minimized
            };
            ApplyNative();
        }

        private void ApplyNative()
        {
            if (_hwnd == IntPtr.Zero) return;

            int ex = NativeMethods.GetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE);
            ex |= NativeMethods.WS_EX_APPWINDOW;      // keep a taskbar button so it can always be found
            ex &= ~NativeMethods.WS_EX_TOOLWINDOW;
            if (S.TakeFocusOnClick) ex &= ~NativeMethods.WS_EX_NOACTIVATE;
            else ex |= NativeMethods.WS_EX_NOACTIVATE; // never take focus from the slideshow
            NativeMethods.SetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE, ex);

            _guard.Enabled = S.AlwaysOnTop;
            _guard.AssertNow();
            RegisterHotkeys(S.GlobalHotkeys);
        }

        private void RegisterHotkeys(bool enable)
        {
            if (_hwnd == IntPtr.Zero) return;
            for (int id = HkStartStop; id <= HkHide; id++) NativeMethods.UnregisterHotKey(_hwnd, id);
            if (!enable) return;

            uint mods = NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT;
            // Failure (key already taken by another program) is ignored; the on-screen buttons still work.
            NativeMethods.RegisterHotKey(_hwnd, HkStartStop, mods, 0x20); // Space
            NativeMethods.RegisterHotKey(_hwnd, HkLap, mods, 0x4C);       // L
            NativeMethods.RegisterHotKey(_hwnd, HkReset, mods, 0x52);     // R
            NativeMethods.RegisterHotKey(_hwnd, HkHide, mods, 0x48);      // H
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY)
            {
                switch (wParam.ToInt32())
                {
                    case HkStartStop: ToggleStartStop(); break;
                    case HkLap: RecordLap(); break;
                    case HkReset: ResetTimer(); break;
                    case HkHide: ToggleVisibility(); break;
                }
                handled = true;
            }
            else if (msg == NativeMethods.WM_MOUSEACTIVATE && !S.TakeFocusOnClick)
            {
                // Clicks are still delivered to our buttons, but the window never becomes active,
                // so the PowerPoint slideshow keeps keyboard focus.
                handled = true;
                return new IntPtr(NativeMethods.MA_NOACTIVATE);
            }
            return IntPtr.Zero;
        }

        private void RestorePosition()
        {
            double left = S.Left.HasValue ? S.Left.Value : double.NaN;
            double top = S.Top.HasValue ? S.Top.Value : double.NaN;
            double vl = SystemParameters.VirtualScreenLeft, vt = SystemParameters.VirtualScreenTop;
            double vr = vl + SystemParameters.VirtualScreenWidth, vb = vt + SystemParameters.VirtualScreenHeight;

            bool onScreen = !double.IsNaN(left) && !double.IsNaN(top)
                            && left > vl - 50 && left < vr - 50 && top > vt - 10 && top < vb - 50;
            if (!onScreen)
            {
                left = SystemParameters.WorkArea.Right - 380;
                top = SystemParameters.WorkArea.Top + 20;
            }
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }

        // ------------------------------------------------------------------ appearance

        internal static Color ParseColor(string hex, Color fallback)
        {
            try { return (Color)ColorConverter.ConvertFromString(hex); }
            catch { return fallback; }
        }

        private static double Clamp(double v, double lo, double hi)
        {
            return Math.Min(Math.Max(v, lo), hi);
        }

        private void ApplyVisuals()
        {
            double scale = Clamp(S.Scale, 0.5, 3.0);
            ScaleT.ScaleX = scale;
            ScaleT.ScaleY = scale;

            TimeText.FontSize = Clamp(S.TimerFontSize, 16, 200);
            Resources["TextBrush"] = new SolidColorBrush(ParseColor(S.TextColor, Colors.White));

            Color bg = ParseColor(S.BackgroundColor, Color.FromRgb(30, 30, 30));
            bg.A = (byte)(Clamp(S.BackgroundOpacity, 0.05, 1.0) * 255);
            Card.Background = new SolidColorBrush(bg);

            Topmost = S.AlwaysOnTop;
            PinDot.Fill = new SolidColorBrush(S.AlwaysOnTop ? Color.FromRgb(0x3D, 0xDC, 0x84) : Color.FromRgb(0x88, 0x88, 0x88));
            PinText.Text = S.AlwaysOnTop ? "ON TOP" : "NOT ON TOP";
            ModeText.Text = CountdownMode ? "COUNTDOWN" : "STOPWATCH";
        }

        /// <summary>Called by the settings window and menu after any setting changed.</summary>
        public void SettingsChanged()
        {
            if (!_cd.IsRunning && !_cd.HasProgress) ReloadCountdownDuration();
            ApplyVisuals();
            ApplyNative();
            UpdateDisplay();
            RefreshAll();
            _settingsSave.Stop();
            _settingsSave.Start(); // debounced: written 0.5 s after the last change
            if (_settingsWindow != null) _settingsWindow.SyncFromSettings();
        }

        private void ReloadCountdownDuration()
        {
            int secs = Math.Max(1, S.CountdownMinutes * 60 + S.CountdownSeconds);
            _cd.Duration = TimeSpan.FromSeconds(secs);
        }

        private void RefreshAll()
        {
            bool running = IsRunning;
            bool hasProgress = CountdownMode ? (_cd.HasProgress && !_cd.IsFinished) : _sw.Elapsed > TimeSpan.Zero;

            StartButton.Content = (!running && hasProgress) ? "CONTINUE" : "START";
            StartButton.IsEnabled = !running;
            StopButton.IsEnabled = running;
            LapButton.IsEnabled = !CountdownMode && running;
            ModeButton.Content = CountdownMode ? "STOPWATCH MODE" : "COUNTDOWN MODE";
            ModeText.Text = CountdownMode ? "COUNTDOWN" : "STOPWATCH";
            SessionText.Text = string.Format(CultureInfo.InvariantCulture, "SESSION {0} · {1} LAPS",
                D.SessionNumber, AppState.Laps.Count);

            bool compact = S.Compact;
            bool showControls = !compact && S.ShowButtons;
            HeaderRow.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            ButtonsPanel.Visibility = showControls ? Visibility.Visible : Visibility.Collapsed;
            ExtraPanel.Visibility = showControls ? Visibility.Visible : Visibility.Collapsed;
            AdjustPanel.Visibility = (showControls && CountdownMode) ? Visibility.Visible : Visibility.Collapsed;
            LapInfoText.Visibility = (!compact && !CountdownMode && LapInfoText.Text.Length > 0)
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateDisplay()
        {
            string text;
            if (CountdownMode)
            {
                TimeSpan remaining = _cd.Remaining;
                if (_cd.IsRunning && remaining <= TimeSpan.Zero)
                {
                    FinishCountdown();
                    remaining = TimeSpan.Zero;
                }
                text = TimeFormat.Main(TimeSpan.FromSeconds(Math.Ceiling(remaining.TotalSeconds)));
            }
            else
            {
                text = TimeFormat.Main(_sw.Elapsed);
            }
            if (TimeText.Text != text) TimeText.Text = text;
        }

        // ------------------------------------------------------------------ timer actions

        private void ToggleStartStop()
        {
            if (IsRunning) StopTimer(); else StartTimer();
        }

        private void StartTimer()
        {
            if (IsRunning) return;
            if (CountdownMode)
            {
                if (_cd.IsFinished) { _cd.Reset(); ReloadCountdownDuration(); StopAlert(); }
                _cd.Start();
            }
            else
            {
                _sw.Start();
            }
            PlayStartStopSound(SystemSounds.Asterisk);
            _ui.Start();
            _autosave.Start();
            UpdateDisplay();
            RefreshAll();
        }

        private void StopTimer()
        {
            if (!IsRunning) return;
            if (CountdownMode) _cd.Stop(); else _sw.Stop();
            _ui.Stop();
            _autosave.Stop();
            PlayStartStopSound(SystemSounds.Beep);
            UpdateDisplay();
            RefreshAll();
            SaveSession();
        }

        private void PauseSilently()
        {
            _sw.Stop();
            _cd.Stop();
            _ui.Stop();
            _autosave.Stop();
        }

        /// <summary>Return to 00:00:00 immediately and stop. The lap list is kept.</summary>
        private void ResetTimer()
        {
            _ui.Stop();
            _autosave.Stop();
            if (CountdownMode)
            {
                _cd.Reset();
                ReloadCountdownDuration();
            }
            else
            {
                _sw.Reset();
                D.LastLapTotalTicks = 0;
                LapInfoText.Text = "";
            }
            StopAlert();
            UpdateDisplay();
            RefreshAll();
            SaveSession();
        }

        private void RestartTimer()
        {
            ResetTimer();
            StartTimer();
        }

        private void NewSession()
        {
            ResetTimer();
            D.SessionNumber++;
            SaveSession();
            RefreshAll();
        }

        private void ToggleMode()
        {
            PauseSilently();
            StopAlert();
            S.CountdownMode = !S.CountdownMode;
            AppState.SaveSettings();
            ApplyVisuals();
            UpdateDisplay();
            RefreshAll();
            if (_settingsWindow != null) _settingsWindow.SyncFromSettings();
        }

        private void RecordLap()
        {
            if (CountdownMode || !_sw.IsRunning) return;

            TimeSpan total = _sw.Elapsed; // sample the clock once, at the moment LAP was triggered
            TimeSpan previous = TimeSpan.FromTicks(D.LastLapTotalTicks);
            int inSession = AppState.Laps.Count(l => l.Session == D.SessionNumber) + 1;

            var lap = new LapEntry
            {
                Number = AppState.Laps.Count + 1,
                Session = D.SessionNumber,
                Name = "Round " + inSession.ToString(CultureInfo.InvariantCulture),
                LapTime = total - previous,
                TotalTime = total
            };
            AppState.Laps.Add(lap);
            D.LastLapTotalTicks = total.Ticks;

            LapInfoText.Text = string.Format(CultureInfo.InvariantCulture, "#{0}  {1}   (total {2})",
                lap.Number, TimeFormat.Lap(lap.LapTime), TimeFormat.Lap(lap.TotalTime));
            RefreshAll();
            SaveSession();
        }

        private void FinishCountdown()
        {
            _cd.Stop();
            _ui.Stop();
            _autosave.Stop();
            StartAlert();
            RefreshAll();
        }

        private void StartAlert()
        {
            var flash = new DoubleAnimation(0.0, 0.8, TimeSpan.FromMilliseconds(400))
            {
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(8),
                FillBehavior = FillBehavior.Stop
            };
            AlertOverlay.BeginAnimation(UIElement.OpacityProperty, flash);
            if (S.SoundsEnabled && S.SoundOnCountdownEnd) SystemSounds.Exclamation.Play();
        }

        private void StopAlert()
        {
            AlertOverlay.BeginAnimation(UIElement.OpacityProperty, null);
            AlertOverlay.Opacity = 0;
        }

        private static void PlayStartStopSound(SystemSound sound)
        {
            if (S.SoundsEnabled && S.SoundOnStartStop) sound.Play();
        }

        private void SaveSession()
        {
            D.ElapsedTicks = _sw.Elapsed.Ticks;
            AppState.SaveSession();
        }

        private void ToggleVisibility()
        {
            if (IsVisible)
            {
                Hide();
            }
            else
            {
                Show(); // ShowActivated=False plus WS_EX_NOACTIVATE: does not take focus
                if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
                if (_guard != null) _guard.AssertNow();
            }
        }

        // ------------------------------------------------------------------ secondary windows

        private void ShowLaps()
        {
            if (_lapsWindow == null)
            {
                _lapsWindow = new LapsWindow();
                _lapsWindow.Closed += (s, e) => _lapsWindow = null;
            }
            _lapsWindow.Show();
            _lapsWindow.Activate();
        }

        private void ShowSettings()
        {
            if (_settingsWindow == null)
            {
                _settingsWindow = new SettingsWindow(this);
                _settingsWindow.Closed += (s, e) => _settingsWindow = null;
            }
            _settingsWindow.Show();
            _settingsWindow.Activate();
        }

        // ------------------------------------------------------------------ event handlers

        private void Start_Click(object sender, RoutedEventArgs e) { StartTimer(); }
        private void Stop_Click(object sender, RoutedEventArgs e) { StopTimer(); }
        private void Lap_Click(object sender, RoutedEventArgs e) { RecordLap(); }
        private void Reset_Click(object sender, RoutedEventArgs e) { ResetTimer(); }
        private void Restart_Click(object sender, RoutedEventArgs e) { RestartTimer(); }
        private void NewSession_Click(object sender, RoutedEventArgs e) { NewSession(); }
        private void Mode_Click(object sender, RoutedEventArgs e) { ToggleMode(); }
        private void Laps_Click(object sender, RoutedEventArgs e) { ShowLaps(); }
        private void Settings_Click(object sender, RoutedEventArgs e) { ShowSettings(); }
        private void Minimize_Click(object sender, RoutedEventArgs e) { WindowState = WindowState.Minimized; }
        private void Close_Click(object sender, RoutedEventArgs e) { Close(); }

        private void Adjust_Click(object sender, RoutedEventArgs e)
        {
            if (_cd.IsRunning) return;
            var button = (Button)sender;
            int delta = int.Parse((string)button.Tag, CultureInfo.InvariantCulture);
            int secs = Math.Max(1, (int)_cd.Duration.TotalSeconds + delta);
            S.CountdownMinutes = secs / 60;
            S.CountdownSeconds = secs % 60;
            _cd.Duration = TimeSpan.FromSeconds(secs);
            AppState.SaveSettings();
            UpdateDisplay();
            if (_settingsWindow != null) _settingsWindow.SyncFromSettings();
        }

        private void Pin_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            S.AlwaysOnTop = !S.AlwaysOnTop;
            SettingsChanged();
            e.Handled = true;
        }

        private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (S.LockPosition || e.ButtonState != MouseButtonState.Pressed) return;
            try { DragMove(); }
            catch (InvalidOperationException) { }
            if (WindowState == WindowState.Normal)
            {
                S.Left = Left;
                S.Top = Top;
                AppState.SaveSettings();
            }
        }

        private void Card_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            foreach (MenuItem item in Card.ContextMenu.Items.OfType<MenuItem>())
            {
                switch (item.Tag as string)
                {
                    case "compact": item.IsChecked = S.Compact; break;
                    case "buttons": item.IsChecked = S.ShowButtons; break;
                    case "lock": item.IsChecked = S.LockPosition; break;
                    case "top": item.IsChecked = S.AlwaysOnTop; break;
                }
            }
        }

        private void Menu_Click(object sender, RoutedEventArgs e)
        {
            var item = (MenuItem)sender;
            switch (item.Tag as string)
            {
                case "startstop": ToggleStartStop(); break;
                case "lap": RecordLap(); break;
                case "reset": ResetTimer(); break;
                case "restart": RestartTimer(); break;
                case "newsession": NewSession(); break;
                case "compact": S.Compact = item.IsChecked; SettingsChanged(); break;
                case "buttons": S.ShowButtons = item.IsChecked; SettingsChanged(); break;
                case "lock": S.LockPosition = item.IsChecked; SettingsChanged(); break;
                case "top": S.AlwaysOnTop = item.IsChecked; SettingsChanged(); break;
                case "laps": ShowLaps(); break;
                case "settings": ShowSettings(); break;
                case "exit": Close(); break;
            }
        }

        /// <summary>Local shortcuts. They only fire while this window has keyboard focus.</summary>
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.None) return;
            switch (e.Key)
            {
                case Key.Space: ToggleStartStop(); e.Handled = true; break;
                case Key.L: RecordLap(); e.Handled = true; break;
                case Key.R: ResetTimer(); e.Handled = true; break;
                case Key.Escape: WindowState = WindowState.Minimized; e.Handled = true; break;
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            _ui.Stop();
            _autosave.Stop();
            if (WindowState == WindowState.Normal)
            {
                S.Left = Left;
                S.Top = Top;
            }
            AppState.SaveSettings();
            SaveSession();
            RegisterHotkeys(false);
            if (_guard != null) _guard.Dispose();
            if (_source != null) _source.RemoveHook(WndProc);
        }
    }
}
