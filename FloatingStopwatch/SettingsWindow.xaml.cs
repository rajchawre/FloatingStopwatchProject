using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FloatingStopwatch
{
    public partial class SettingsWindow : Window
    {
        private static readonly Regex HexColor = new Regex("^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$");

        private readonly MainWindow _main;
        // True while controls are being filled from settings (and during InitializeComponent),
        // so their change events do not write back or recurse.
        private bool _loading = true;

        private static AppSettings S { get { return AppState.Settings; } }

        public SettingsWindow(MainWindow main)
        {
            _main = main;
            InitializeComponent();
            BuildSwatches(TextSwatches, new[] { "#FFFFFF", "#FFD54F", "#7CFC9A", "#6EC6FF", "#FF8A80", "#202020" }, true);
            BuildSwatches(BgSwatches, new[] { "#1E1E1E", "#000000", "#0D2B45", "#1B3B2B", "#3B1B1B", "#FFFFFF" }, false);
            SyncFromSettings();
        }

        private void BuildSwatches(Panel panel, string[] colors, bool isText)
        {
            foreach (string hex in colors)
            {
                string chosen = hex;
                var swatch = new Button
                {
                    Width = 22,
                    Height = 22,
                    Margin = new Thickness(0, 0, 6, 0),
                    Focusable = false,
                    ToolTip = hex,
                    BorderBrush = Brushes.Gray,
                    Background = new SolidColorBrush(MainWindow.ParseColor(hex, Colors.Gray))
                };
                swatch.Click += (s, e) =>
                {
                    if (isText) S.TextColor = chosen; else S.BackgroundColor = chosen;
                    _main.SettingsChanged();
                };
                panel.Children.Add(swatch);
            }
        }

        /// <summary>Refresh every control from the current settings (also called by the main window).</summary>
        public void SyncFromSettings()
        {
            _loading = true;
            try
            {
                SliderScale.Value = S.Scale;
                SliderFont.Value = S.TimerFontSize;
                SliderOpacity.Value = S.BackgroundOpacity;
                ScaleLabel.Text = Math.Round(S.Scale * 100).ToString(CultureInfo.InvariantCulture) + "%";
                FontLabel.Text = Math.Round(S.TimerFontSize).ToString(CultureInfo.InvariantCulture);
                OpacityLabel.Text = Math.Round(S.BackgroundOpacity * 100).ToString(CultureInfo.InvariantCulture) + "%";

                if (TextColorBox.Text != S.TextColor) TextColorBox.Text = S.TextColor;
                if (BgColorBox.Text != S.BackgroundColor) BgColorBox.Text = S.BackgroundColor;

                ChkTop.IsChecked = S.AlwaysOnTop;
                ChkButtons.IsChecked = S.ShowButtons;
                ChkCompact.IsChecked = S.Compact;
                ChkLock.IsChecked = S.LockPosition;
                ChkHotkeys.IsChecked = S.GlobalHotkeys;
                ChkFocus.IsChecked = S.TakeFocusOnClick;

                ChkSounds.IsChecked = S.SoundsEnabled;
                ChkSoundStartStop.IsChecked = S.SoundOnStartStop;
                ChkSoundEnd.IsChecked = S.SoundOnCountdownEnd;
                ChkSoundStartStop.IsEnabled = S.SoundsEnabled;
                ChkSoundEnd.IsEnabled = S.SoundsEnabled;

                int value;
                if (!int.TryParse(MinBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out value) || value != S.CountdownMinutes)
                    MinBox.Text = S.CountdownMinutes.ToString(CultureInfo.InvariantCulture);
                if (!int.TryParse(SecBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out value) || value != S.CountdownSeconds)
                    SecBox.Text = S.CountdownSeconds.ToString(CultureInfo.InvariantCulture);
            }
            finally
            {
                _loading = false;
            }
        }

        private void Slider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading) return;
            S.Scale = Math.Round(SliderScale.Value, 2);
            S.TimerFontSize = Math.Round(SliderFont.Value);
            S.BackgroundOpacity = Math.Round(SliderOpacity.Value, 2);
            _main.SettingsChanged();
        }

        private void Option_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            S.AlwaysOnTop = ChkTop.IsChecked == true;
            S.ShowButtons = ChkButtons.IsChecked == true;
            S.Compact = ChkCompact.IsChecked == true;
            S.LockPosition = ChkLock.IsChecked == true;
            S.GlobalHotkeys = ChkHotkeys.IsChecked == true;
            S.TakeFocusOnClick = ChkFocus.IsChecked == true;
            S.SoundsEnabled = ChkSounds.IsChecked == true;
            S.SoundOnStartStop = ChkSoundStartStop.IsChecked == true;
            S.SoundOnCountdownEnd = ChkSoundEnd.IsChecked == true;
            _main.SettingsChanged();
        }

        private void ColorBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading) return;
            bool textOk = HexColor.IsMatch(TextColorBox.Text);
            bool bgOk = HexColor.IsMatch(BgColorBox.Text);
            TextColorBox.Foreground = textOk ? Brushes.Black : Brushes.Red;
            BgColorBox.Foreground = bgOk ? Brushes.Black : Brushes.Red;
            if (textOk) S.TextColor = TextColorBox.Text;
            if (bgOk) S.BackgroundColor = BgColorBox.Text;
            if (textOk || bgOk) _main.SettingsChanged();
        }

        private void Countdown_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading) return;
            int minutes, seconds;
            if (!int.TryParse(MinBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out minutes)) return;
            if (!int.TryParse(SecBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out seconds)) return;
            if (minutes > 999 || seconds > 59) return;
            if (minutes == 0 && seconds == 0) return; // a countdown needs at least one second
            S.CountdownMinutes = minutes;
            S.CountdownSeconds = seconds;
            _main.SettingsChanged();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
