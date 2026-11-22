using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace FloatingStopwatch
{
    public partial class LapsWindow : Window
    {
        public LapsWindow()
        {
            InitializeComponent();
            LapGrid.ItemsSource = AppState.Laps;
        }

        private void LapGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // Save after the edited name has been committed to the LapEntry.
            Dispatcher.BeginInvoke(new Action(AppState.SaveSession),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var selected = LapGrid.SelectedItems.Cast<LapEntry>().ToList();
            if (selected.Count == 0) return;
            AppState.RemoveLaps(selected);
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            if (AppState.Laps.Count == 0) return;
            MessageBoxResult answer = MessageBox.Show(this,
                "Delete all " + AppState.Laps.Count.ToString(CultureInfo.InvariantCulture) + " entries from the lap list?",
                "Clear lap list", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Yes) AppState.ClearLaps();
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            if (AppState.Laps.Count == 0)
            {
                MessageBox.Show(this, "The lap list is empty.", "Export CSV",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export lap list",
                Filter = "CSV file (*.csv)|*.csv",
                DefaultExt = ".csv",
                FileName = "stopwatch-laps-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".csv"
            };
            if (dialog.ShowDialog(this) != true) return;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("No.,Session,Activity / Lap,Lap Time,Total Elapsed,Lap Seconds,Total Seconds");
                foreach (LapEntry lap in AppState.Laps)
                {
                    sb.Append(lap.Number.ToString(CultureInfo.InvariantCulture)).Append(',');
                    sb.Append(lap.Session.ToString(CultureInfo.InvariantCulture)).Append(',');
                    sb.Append(CsvEscape(lap.Name)).Append(',');
                    sb.Append(TimeFormat.Lap(lap.LapTime)).Append(',');
                    sb.Append(TimeFormat.Lap(lap.TotalTime)).Append(',');
                    sb.Append(lap.LapTime.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture)).Append(',');
                    sb.AppendLine(lap.TotalTime.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture));
                }
                // UTF-8 with BOM so Excel reads non-English activity names correctly.
                File.WriteAllText(dialog.FileName, sb.ToString(), new UTF8Encoding(true));
                MessageBox.Show(this, "Exported " + AppState.Laps.Count.ToString(CultureInfo.InvariantCulture) +
                    " entries to:\n" + dialog.FileName, "Export CSV", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not write the file:\n" + ex.Message, "Export CSV",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private static string CsvEscape(string value)
        {
            if (value == null) value = "";
            // Stop Excel treating a name that starts with = + - @ as a formula.
            if (value.Length > 0 && "=+-@".IndexOf(value[0]) >= 0) value = "'" + value;
            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
                value = "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }
    }
}
