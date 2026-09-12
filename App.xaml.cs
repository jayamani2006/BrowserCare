using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace BrowserCare
{
    public partial class App : Application
    {
        public App()
        {
            // The app was crashing on launch with no visible window and no
            // diagnostic output — a bare ".NET unhandled exception" exit code.
            // Catch every unhandled exception path (UI thread, background
            // threads, and the task scheduler) and write the real error to a
            // log file plus a message box instead of dying silently.
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            CleanupOrphanedTempFiles();
        }

        /// <summary>
        /// History/Downloads/Cookies reads copy the live database to a temp file
        /// and delete it when done. If a prior run crashed mid-read, that copy —
        /// which can contain browsing history — could be left behind in the
        /// shared %TEMP% folder. Clean up anything BrowserCare left there on
        /// every startup rather than relying solely on the delete-after-use path.
        /// </summary>
        private static void CleanupOrphanedTempFiles()
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(Path.GetTempPath(), "browsercare_*.sqlite"))
                {
                    try { File.Delete(file); } catch { /* best effort — not worth failing startup over */ }
                }
            }
            catch
            {
                // Non-fatal — %TEMP% itself being inaccessible shouldn't block startup.
            }
        }

        private static string LogPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BrowserCare", "crash.log");

        private static void LogAndShow(Exception? ex, string source)
        {
            try
            {
                var dir = Path.GetDirectoryName(LogPath)!;
                Directory.CreateDirectory(dir);

                // Cap the log at ~1MB so a repeated crash can't grow it without bound —
                // keep the most recent half rather than truncating mid-entry.
                const long maxBytes = 1_000_000;
                if (File.Exists(LogPath) && new FileInfo(LogPath).Length > maxBytes)
                {
                    var existing = File.ReadAllText(LogPath);
                    File.WriteAllText(LogPath, existing[(existing.Length / 2)..]);
                }

                File.AppendAllText(LogPath,
                    $"---- {DateTime.Now:yyyy-MM-dd HH:mm:ss} ({source}) ----{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
            }
            catch
            {
                // If we can't even write the log, there's nothing more we can do here.
            }

            MessageBox.Show(
                $"BrowserCare hit an unexpected error and needs to close.\n\n" +
                $"{ex?.GetType().Name}: {ex?.Message}\n\n" +
                $"Details were saved to:\n{LogPath}",
                "BrowserCare — Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogAndShow(e.Exception, "UI thread");
            e.Handled = true; // keep the app alive if at all possible instead of hard-crashing
        }

        private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogAndShow(e.ExceptionObject as Exception, "AppDomain");
        }

        private void OnUnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
        {
            LogAndShow(e.Exception, "Task");
            e.SetObserved();
        }
    }
}
