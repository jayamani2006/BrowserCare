using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BrowserCare.Core;

namespace BrowserCare.Chrome
{
    /// <summary>
    /// Discovers every actual Chrome profile directory under User Data.
    /// Does not assume "Profile 1, Profile 2..." sequencing — reads what's really there.
    /// </summary>
    public sealed class ProfileScanner
    {
        // Folders under User Data that are never profiles.
        private static readonly HashSet<string> NonProfileFolders = new(StringComparer.OrdinalIgnoreCase)
        {
            "System Profile", "Crashpad", "CrashpadMetrics", "GrShaderCache",
            "ShaderCache", "GraphiteDawnCache", "GPUCache", "Component Cache",
            "OptimizationGuidePredictionModels", "Safe Browsing", "SwReporter",
            "WidevineCdm", "extensions_crx_cache", "segmentation_platform"
        };

        public List<ChromeProfile> DiscoverProfiles(string userDataPath)
        {
            var results = new List<ChromeProfile>();
            if (!Directory.Exists(userDataPath))
                return results;

            // A real Chrome profile directory always contains a "Preferences" file.
            IEnumerable<string> candidateDirs;
            try
            {
                candidateDirs = Directory.EnumerateDirectories(userDataPath);
            }
            catch (UnauthorizedAccessException)
            {
                return results;
            }

            foreach (var dir in candidateDirs)
            {
                var folderName = Path.GetFileName(dir);
                if (NonProfileFolders.Contains(folderName))
                    continue;

                var preferencesFile = Path.Combine(dir, "Preferences");
                if (!File.Exists(preferencesFile))
                    continue;

                var profile = new ChromeProfile
                {
                    FolderName = folderName,
                    FullPath = dir,
                    DisplayName = ReadProfileDisplayName(preferencesFile) ?? folderName,
                    AccountLabel = ReadAccountLabel(preferencesFile) ?? "Account information unavailable"
                };

                try
                {
                    profile.LastModifiedUtc = Directory.GetLastWriteTimeUtc(dir);
                }
                catch
                {
                    // Non-fatal — leave null if unavailable.
                }

                results.Add(profile);
            }

            return results;
        }

        /// <summary>
        /// Reads the Chrome-assigned profile display name from Preferences (profile.name).
        /// Never fabricates a name.
        /// </summary>
        private static string? ReadProfileDisplayName(string preferencesPath)
        {
            try
            {
                using var stream = new FileStream(preferencesPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var doc = JsonDocument.Parse(stream);
                if (doc.RootElement.TryGetProperty("profile", out var profileEl) &&
                    profileEl.TryGetProperty("name", out var nameEl) &&
                    nameEl.ValueKind == JsonValueKind.String)
                {
                    return nameEl.GetString();
                }
            }
            catch
            {
                // Locked, corrupted, or unreadable — caller falls back to folder name.
            }
            return null;
        }

        /// <summary>
        /// Attempts to read a locally-available account identifier (e.g. account_info email)
        /// from Preferences. Returns null rather than guessing when unavailable — BrowserCare
        /// never infers an email address from a folder name.
        /// </summary>
        private static string? ReadAccountLabel(string preferencesPath)
        {
            try
            {
                using var stream = new FileStream(preferencesPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var doc = JsonDocument.Parse(stream);

                if (doc.RootElement.TryGetProperty("account_info", out var accountInfoEl) &&
                    accountInfoEl.ValueKind == JsonValueKind.Array)
                {
                    var first = accountInfoEl.EnumerateArray().FirstOrDefault();
                    if (first.ValueKind == JsonValueKind.Object &&
                        first.TryGetProperty("email", out var emailEl) &&
                        emailEl.ValueKind == JsonValueKind.String)
                    {
                        return emailEl.GetString();
                    }
                }
            }
            catch
            {
                // Not available locally — leave as "unavailable" rather than guess.
            }
            return null;
        }
    }
}
