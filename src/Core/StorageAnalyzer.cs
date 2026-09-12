using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BrowserCare.Core
{
    /// <summary>
    /// Computes real folder sizes on disk. No estimates, no fake numbers.
    /// Handles locked/inaccessible files gracefully instead of crashing.
    /// </summary>
    public sealed class StorageAnalyzer
    {
        /// <summary>
        /// Recursively sums file sizes under <paramref name="path"/>.
        /// Skips files/folders it cannot access and records them via <paramref name="skipped"/>.
        /// </summary>
        public async Task<long> GetDirectorySizeAsync(
            string path,
            CancellationToken cancellationToken = default,
            Action<string>? skipped = null)
        {
            if (!Directory.Exists(path))
                return 0;

            return await Task.Run(() => GetDirectorySize(path, cancellationToken, skipped), cancellationToken);
        }

        private long GetDirectorySize(string path, CancellationToken token, Action<string>? skipped)
        {
            long total = 0;

            IEnumerable<string>? files = null;
            try
            {
                files = Directory.EnumerateFiles(path);
            }
            catch (UnauthorizedAccessException)
            {
                skipped?.Invoke(path);
                return 0;
            }
            catch (IOException)
            {
                skipped?.Invoke(path);
                return 0;
            }

            foreach (var file in files)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    var info = new FileInfo(file);
                    total += info.Length;
                }
                catch (IOException)
                {
                    // File deleted mid-scan, or locked — skip, don't crash.
                    skipped?.Invoke(file);
                }
                catch (UnauthorizedAccessException)
                {
                    skipped?.Invoke(file);
                }
            }

            IEnumerable<string>? subDirs = null;
            try
            {
                subDirs = Directory.EnumerateDirectories(path);
            }
            catch (UnauthorizedAccessException)
            {
                skipped?.Invoke(path);
                return total;
            }

            foreach (var dir in subDirs)
            {
                token.ThrowIfCancellationRequested();
                total += GetDirectorySize(dir, token, skipped);
            }

            return total;
        }
    }
}
