using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BrowserCare.Chrome;

namespace BrowserCare.Core
{
    public enum ScanMode { Quick, Deep, Profile }

    /// <summary>
    /// Orchestrates a scan: Chrome detection → profile discovery → per-category sizing → totals.
    /// Reports live progress instead of faking a percentage.
    /// </summary>
    public sealed class ScanEngine
    {
        private readonly ChromeDetector _detector = new();
        private readonly ProfileScanner _profileScanner = new();
        private readonly StorageAnalyzer _storage = new();

        // Categories checked in Quick Scan — the disposable/cache-only set.
        private static readonly string[] QuickScanFolders =
        {
            "Cache", "Code Cache", "GPUCache", "GrShaderCache", "ShaderCache",
            "Media Cache", "Component Cache", "OptimizationGuidePredictionModels"
        };

        // Additional categories examined only in Deep/Profile scans.
        private static readonly string[] DeepScanFolders =
        {
            "IndexedDB", "Local Storage", "Session Storage", "File System",
            "Storage", "Favicons", "Service Worker", "Extensions"
        };

        public async Task<ScanResult> ScanAsync(
            ScanMode mode,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new ScanResult();

            progress?.Report("Detecting Chrome...");
            var userDataPath = _detector.FindUserDataDirectory();
            if (userDataPath == null)
            {
                result.ChromeDetected = false;
                return result;
            }

            result.ChromeDetected = true;
            result.ChromeUserDataPath = userDataPath;

            progress?.Report("Finding profiles...");
            var profiles = _profileScanner.DiscoverProfiles(userDataPath);

            var foldersToScan = mode == ScanMode.Quick
                ? QuickScanFolders
                : Array.Empty<string>(); // Deep/Profile scans below add both sets explicitly.

            foreach (var profile in profiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report($"Scanning {profile.DisplayName}...");

                var categories = mode == ScanMode.Quick
                    ? QuickScanFolders
                    : ConcatArrays(QuickScanFolders, DeepScanFolders);

                foreach (var relativeFolder in categories)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Support one level of nesting, e.g. "Service Worker\CacheStorage".
                    var fullPath = Path.Combine(profile.FullPath, relativeFolder.Replace('\\', Path.DirectorySeparatorChar));
                    if (!Directory.Exists(fullPath))
                        continue;

                    progress?.Report($"{profile.DisplayName} → {relativeFolder}");

                    var size = await _storage.GetDirectorySizeAsync(
                        fullPath,
                        cancellationToken,
                        skipped => result.SkippedItems.Add(skipped));

                    if (size == 0)
                        continue;

                    var (level, description, consequences) = SafetyEngine.Classify(relativeFolder);

                    var item = new ScanItem
                    {
                        Name = relativeFolder,
                        Path = fullPath,
                        SizeBytes = size,
                        Safety = level,
                        Description = description,
                        Consequences = consequences,
                        CanClean = SafetyEngine.IsCleanableBySafeCleanup(level),
                        ProfileId = profile.FolderName
                    };

                    profile.Items.Add(item);
                    profile.TotalSizeBytes += size;

                    switch (level)
                    {
                        case SafetyLevel.Safe: result.SafeCleanupBytes += size; break;
                        case SafetyLevel.Review: result.ReviewBytes += size; break;
                        case SafetyLevel.Protected: result.ProtectedBytes += size; break;
                    }

                    result.TotalSizeBytes += size;
                }

                result.Profiles.Add(profile);
            }

            progress?.Report("Scan complete.");
            return result;
        }

        private static string[] ConcatArrays(string[] a, string[] b)
        {
            var combined = new string[a.Length + b.Length];
            a.CopyTo(combined, 0);
            b.CopyTo(combined, a.Length);
            return combined;
        }
    }
}
