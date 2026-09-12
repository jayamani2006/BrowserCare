using System;
using System.IO;
using System.Text.Json;

namespace BrowserCare.Core
{
    public sealed class AppSettings
    {
        public bool StartWithWindows { get; set; }
        public bool RequireCleanupConfirmation { get; set; } = true;
        public bool CreateBackupsBeforeReviewCleanup { get; set; } = true;
        public bool IncludeInactiveProfiles { get; set; } = true;
    }

    /// <summary>
    /// Loads and saves real user settings to %LOCALAPPDATA%\BrowserCare\settings.json.
    /// No telemetry, no network access — purely local.
    /// </summary>
    public static class SettingsManager
    {
        private static string SettingsPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "BrowserCare", "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                    if (loaded != null) return loaded;
                }
            }
            catch
            {
                // Corrupted/unreadable settings — fall back to defaults rather than crash.
            }
            return new AppSettings();
        }

        public static void Save(AppSettings settings)
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
    }
}
