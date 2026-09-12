using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using BrowserCare.Core;

namespace BrowserCare.Chrome
{
    public sealed class ExtensionInfo
    {
        public required string ExtensionId { get; init; }
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string SizeDisplay => ScanItem.FormatBytes(SizeBytes);
        public required string ProfileFolder { get; init; }
    }

    /// <summary>
    /// Lists installed extensions per profile by reading the real Extensions
    /// folder and each extension's manifest.json — never invents a name.
    /// </summary>
    public sealed class ExtensionAnalyzer
    {
        private readonly StorageAnalyzer _storage = new();

        public async Task<List<ExtensionInfo>> ScanProfileAsync(ChromeProfile profile)
        {
            var results = new List<ExtensionInfo>();
            var extensionsRoot = Path.Combine(profile.FullPath, "Extensions");
            if (!Directory.Exists(extensionsRoot))
                return results;

            IEnumerable<string> idFolders;
            try
            {
                idFolders = Directory.EnumerateDirectories(extensionsRoot);
            }
            catch (UnauthorizedAccessException)
            {
                return results;
            }

            foreach (var idFolder in idFolders)
            {
                var extensionId = Path.GetFileName(idFolder);

                // Extensions are stored as Extensions\<id>\<version>\...
                string[] versionFolders;
                try
                {
                    versionFolders = Directory.GetDirectories(idFolder);
                }
                catch
                {
                    continue;
                }

                if (versionFolders.Length == 0)
                    continue;

                // Use the most recently modified version folder if more than one exists.
                var versionFolder = versionFolders[0];
                foreach (var vf in versionFolders)
                {
                    if (Directory.GetLastWriteTimeUtc(vf) > Directory.GetLastWriteTimeUtc(versionFolder))
                        versionFolder = vf;
                }

                var name = ReadManifestName(versionFolder) ?? extensionId;
                var version = Path.GetFileName(versionFolder);

                long size = 0;
                try
                {
                    // This used to be a blocking .GetAwaiter().GetResult() call, which
                    // deadlocked on the UI thread (the awaited Task.Run tries to resume
                    // back on the same UI thread that's blocked waiting for it). That was
                    // the actual cause of "extension scan not working" — the whole app
                    // just hung with no error. Awaiting properly fixes it.
                    size = await _storage.GetDirectorySizeAsync(idFolder);
                }
                catch
                {
                    // Non-fatal — leave as 0 if it can't be measured.
                }

                results.Add(new ExtensionInfo
                {
                    ExtensionId = extensionId,
                    Name = name,
                    Version = version,
                    SizeBytes = size,
                    ProfileFolder = profile.FolderName
                });
            }

            return results;
        }

        private static string? ReadManifestName(string versionFolder)
        {
            var manifestPath = Path.Combine(versionFolder, "manifest.json");
            if (!File.Exists(manifestPath))
                return null;

            try
            {
                using var stream = File.OpenRead(manifestPath);
                using var doc = JsonDocument.Parse(stream);
                if (doc.RootElement.TryGetProperty("name", out var nameEl) &&
                    nameEl.ValueKind == JsonValueKind.String)
                {
                    var name = nameEl.GetString() ?? string.Empty;

                    // Localized manifests use "__MSG_key__" placeholders that require
                    // reading _locales/<lang>/messages.json to resolve. Rather than
                    // guessing, show the raw key alongside a note — still real data.
                    if (name.StartsWith("__MSG_", StringComparison.OrdinalIgnoreCase))
                    {
                        var resolved = TryResolveLocalizedName(versionFolder, name);
                        return resolved ?? name;
                    }
                    return name;
                }
            }
            catch
            {
                // Corrupted/locked manifest — fall back to the extension ID upstream.
            }
            return null;
        }

        private static string? TryResolveLocalizedName(string versionFolder, string msgKey)
        {
            var key = msgKey.Trim('_').Substring(4); // strip "MSG_" after trimming underscores
            var localesRoot = Path.Combine(versionFolder, "_locales");
            if (!Directory.Exists(localesRoot))
                return null;

            // Prefer "en" if present, otherwise take the first available locale.
            var preferred = Path.Combine(localesRoot, "en", "messages.json");
            var messagesFile = File.Exists(preferred)
                ? preferred
                : Directory.EnumerateFiles(localesRoot, "messages.json", SearchOption.AllDirectories).FirstOrDefaultSafe();

            if (messagesFile == null || !File.Exists(messagesFile))
                return null;

            try
            {
                using var stream = File.OpenRead(messagesFile);
                using var doc = JsonDocument.Parse(stream);
                if (doc.RootElement.TryGetProperty(key, out var entry) &&
                    entry.TryGetProperty("message", out var msgEl))
                {
                    return msgEl.GetString();
                }
            }
            catch
            {
                // Leave unresolved.
            }
            return null;
        }
    }

    internal static class EnumerableExtensions
    {
        public static string? FirstOrDefaultSafe(this IEnumerable<string> source)
        {
            foreach (var item in source) return item;
            return null;
        }
    }
}
