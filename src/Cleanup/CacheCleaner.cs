using System;
using System.Collections.Generic;
using System.IO;
using BrowserCare.Core;

namespace BrowserCare.Cleanup
{
    public sealed class CleanupLogEntry
    {
        public required string Item { get; init; }
        public long SizeBytes { get; init; }
        public bool Success { get; init; }
        public string? Error { get; init; }
    }

    public sealed class CleanupResult
    {
        public long RecoveredBytes { get; set; }
        public List<CleanupLogEntry> Log { get; } = new();
    }

    /// <summary>
    /// Deletes only items the SafetyEngine has classified as Safe. Never accepts
    /// an arbitrary user-entered path — only ScanItem objects that already carry
    /// a Safe classification from a completed scan.
    /// </summary>
    public sealed class CacheCleaner
    {
        public CleanupResult CleanSafeItems(IEnumerable<ScanItem> items)
        {
            var result = new CleanupResult();

            foreach (var item in items)
            {
                if (item.Safety != SafetyLevel.Safe)
                {
                    // Defensive guard — Safe Cleanup must never touch Review/Protected items,
                    // even if something upstream passed one in by mistake.
                    result.Log.Add(new CleanupLogEntry
                    {
                        Item = item.Name,
                        SizeBytes = 0,
                        Success = false,
                        Error = "Skipped — not classified Safe."
                    });
                    continue;
                }

                try
                {
                    var freed = DeleteContentsOnly(item.Path);
                    result.RecoveredBytes += freed;
                    result.Log.Add(new CleanupLogEntry { Item = item.Name, SizeBytes = freed, Success = true });
                }
                catch (Exception ex)
                {
                    result.Log.Add(new CleanupLogEntry
                    {
                        Item = item.Name,
                        SizeBytes = 0,
                        Success = false,
                        Error = ex.Message
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// Deletes the contents of a cache folder but keeps the folder itself,
        /// since Chrome expects these directories to exist and will recreate
        /// files inside them as needed.
        /// </summary>
        private static long DeleteContentsOnly(string folderPath)
        {
            long freed = 0;
            if (!Directory.Exists(folderPath))
                return 0;

            foreach (var file in Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var info = new FileInfo(file);
                    var size = info.Length;
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                    freed += size;
                }
                catch (IOException)
                {
                    // Locked file (Chrome still using it) — skip, don't crash.
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip inaccessible files.
                }
            }

            // Remove now-empty subdirectories, deepest first, but keep the top-level folder.
            var allDirs = new List<string>(Directory.EnumerateDirectories(folderPath, "*", SearchOption.AllDirectories));
            allDirs.Sort((a, b) => b.Length.CompareTo(a.Length)); // deepest paths first
            foreach (var dir in allDirs)
            {
                try
                {
                    if (Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length == 0)
                        Directory.Delete(dir);
                }
                catch
                {
                    // Non-fatal — leave it for next cleanup.
                }
            }

            return freed;
        }
    }
}
