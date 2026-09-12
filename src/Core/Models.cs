using System;
using System.Collections.Generic;

namespace BrowserCare.Core
{
    /// <summary>
    /// Safety classification for a cleanup item.
    /// Controls what the UI is allowed to delete — never bypassed by UI code directly.
    /// </summary>
    public enum SafetyLevel
    {
        Safe,       // Green — Chrome can recreate this data
        Review,     // Yellow — may affect website data, ask the user
        Protected   // Red — never removed by Safe Cleanup
    }

    /// <summary>
    /// A single scanned storage item (a folder, a category, or a per-site entry)
    /// with its safety classification and human-readable explanation.
    /// </summary>
    public sealed class ScanItem
    {
        public required string Name { get; init; }
        public required string Path { get; init; }
        public long SizeBytes { get; set; }
        public SafetyLevel Safety { get; set; } = SafetyLevel.Review;
        public string Description { get; set; } = string.Empty;
        public string Consequences { get; set; } = string.Empty;
        public bool CanClean { get; set; }
        public string? ProfileId { get; set; }

        public string SizeDisplay => FormatBytes(SizeBytes);

        public static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int unitIndex = 0;
            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }
            return $"{size:0.##} {units[unitIndex]}";
        }
    }

    /// <summary>
    /// A detected Chrome profile directory (e.g. "Default", "Profile 15").
    /// </summary>
    public sealed class ChromeProfile
    {
        public required string FolderName { get; init; }
        public required string FullPath { get; init; }
        public string DisplayName { get; set; } = "Chrome Profile";
        public string AccountLabel { get; set; } = "Account information unavailable";
        public long TotalSizeBytes { get; set; }
        public DateTime? LastModifiedUtc { get; set; }
        public List<ScanItem> Items { get; } = new();

        public string SizeDisplay => ScanItem.FormatBytes(TotalSizeBytes);
    }

    /// <summary>
    /// Result of a full or partial Chrome scan.
    /// </summary>
    public sealed class ScanResult
    {
        public bool ChromeDetected { get; set; }
        public string? ChromeUserDataPath { get; set; }
        public List<ChromeProfile> Profiles { get; } = new();
        public long TotalSizeBytes { get; set; }
        public long SafeCleanupBytes { get; set; }
        public long ReviewBytes { get; set; }
        public long ProtectedBytes { get; set; }
        public DateTime ScannedAtUtc { get; set; } = DateTime.UtcNow;
        public List<string> SkippedItems { get; } = new();

        public string TotalSizeDisplay => ScanItem.FormatBytes(TotalSizeBytes);
        public string SafeCleanupDisplay => ScanItem.FormatBytes(SafeCleanupBytes);
    }
}
