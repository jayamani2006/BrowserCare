using System;
using System.IO;

namespace BrowserCare.Chrome
{
    /// <summary>
    /// Detects the current Windows user's Chrome installation and User Data directory.
    /// Never hard-codes a username — always resolves via environment variables.
    /// </summary>
    public sealed class ChromeDetector
    {
        /// <summary>
        /// Resolves Chrome's "User Data" directory for the current user, if present.
        /// Standard path: %LOCALAPPDATA%\Google\Chrome\User Data
        /// </summary>
        public string? FindUserDataDirectory()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(localAppData))
                return null;

            var standardPath = Path.Combine(localAppData, "Google", "Chrome", "User Data");
            if (Directory.Exists(standardPath))
                return standardPath;

            // Chrome Beta / Dev / Canary fallback channels — still the same current user.
            string[] altChannelFolders =
            {
                Path.Combine(localAppData, "Google", "Chrome Beta", "User Data"),
                Path.Combine(localAppData, "Google", "Chrome Dev", "User Data"),
                Path.Combine(localAppData, "Google", "Chrome SxS", "User Data"), // Canary
            };

            foreach (var candidate in altChannelFolders)
            {
                if (Directory.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        /// <summary>True if a Chrome User Data directory was found for the current user.</summary>
        public bool IsChromeInstalled() => FindUserDataDirectory() != null;

        /// <summary>
        /// Checks whether Chrome is currently running, so cleanup can warn the user
        /// that some files are locked while Chrome is open.
        /// </summary>
        public bool IsChromeRunning()
        {
            try
            {
                var processes = System.Diagnostics.Process.GetProcessesByName("chrome");
                return processes.Length > 0;
            }
            catch
            {
                // If we can't determine process state, err on the side of caution
                // and let the caller treat it as "unknown" rather than crash.
                return false;
            }
        }
    }
}
