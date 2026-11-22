using System.Threading;
using System.Windows;

namespace FloatingStopwatch
{
    public partial class App : Application
    {
        private Mutex _singleInstance;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            bool createdNew;
            _singleInstance = new Mutex(true, "FloatingStopwatch.SingleInstance.7c1e4b", out createdNew);
            if (!createdNew)
            {
                MessageBox.Show(
                    "Floating Stopwatch is already running.\n\nIf you cannot see it, press Ctrl+Alt+H to show it, " +
                    "or look for it in the taskbar.",
                    "Floating Stopwatch", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            DispatcherUnhandledException += (s, ex) =>
            {
                LogError(ex.Exception);
                ex.Handled = true; // keep the stopwatch running if possible
            };

            try
            {
                AppState.Load();
                new MainWindow().Show();
            }
            catch (System.Exception ex)
            {
                LogError(ex);
                MessageBox.Show("Floating Stopwatch could not start:\n\n" + ex.Message +
                    "\n\nDetails were written to %AppData%\\FloatingStopwatch\\error.log",
                    "Floating Stopwatch", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private static void LogError(System.Exception ex)
        {
            try
            {
                string dir = System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "FloatingStopwatch");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "error.log"),
                    System.DateTime.Now.ToString("s") + "  " + ex + "\r\n\r\n");
            }
            catch { }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_singleInstance != null)
            {
                try { _singleInstance.ReleaseMutex(); } catch { }
                _singleInstance.Dispose();
            }
            base.OnExit(e);
        }
    }
}
