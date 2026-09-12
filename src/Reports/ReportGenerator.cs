using System;
using System.IO;
using System.Text;
using BrowserCare.Core;

namespace BrowserCare.Reports
{
    /// <summary>
    /// Generates a plain-text cleanup/scan report from real scan data.
    /// JSON/HTML export formats are planned for a later phase — this
    /// generator only claims to produce what it actually produces.
    /// </summary>
    public sealed class ReportGenerator
    {
        public string GenerateText(ScanResult result, long? recoveredBytes = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("BrowserCare Scan Report");
            sb.AppendLine(new string('=', 32));
            sb.AppendLine($"Scan date: {result.ScannedAtUtc.ToLocalTime():dd MMMM yyyy, HH:mm}");
            sb.AppendLine();

            if (!result.ChromeDetected)
            {
                sb.AppendLine("Google Chrome was not found on this computer.");
                return sb.ToString();
            }

            sb.AppendLine($"Profiles scanned: {result.Profiles.Count}");
            sb.AppendLine($"Total storage examined: {result.TotalSizeDisplay}");
            sb.AppendLine($"Safe to clean: {ScanItem.FormatBytes(result.SafeCleanupBytes)}");
            sb.AppendLine($"Needs review: {ScanItem.FormatBytes(result.ReviewBytes)}");
            sb.AppendLine($"Protected: {ScanItem.FormatBytes(result.ProtectedBytes)}");

            if (recoveredBytes.HasValue)
                sb.AppendLine($"Recovered by Safe Cleanup: {ScanItem.FormatBytes(recoveredBytes.Value)}");

            sb.AppendLine();
            sb.AppendLine("Profiles");
            sb.AppendLine(new string('-', 32));
            foreach (var profile in result.Profiles)
            {
                sb.AppendLine($"{profile.DisplayName} ({profile.FolderName}) — {profile.SizeDisplay}");
                foreach (var item in profile.Items)
                {
                    sb.AppendLine($"    {item.Name,-24} {item.SizeDisplay,10}  [{item.Safety}]");
                }
            }

            if (result.SkippedItems.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"Skipped (locked/inaccessible) items: {result.SkippedItems.Count}");
            }

            return sb.ToString();
        }

        /// <summary>Writes the report to the given directory and returns the full file path.</summary>
        public string SaveToFile(ScanResult result, string directory, long? recoveredBytes = null)
        {
            Directory.CreateDirectory(directory);
            var fileName = $"BrowserCare-Report-{DateTime.Now:yyyyMMdd-HHmmss}.txt";
            var fullPath = Path.Combine(directory, fileName);
            File.WriteAllText(fullPath, GenerateText(result, recoveredBytes));
            return fullPath;
        }
    }
}
