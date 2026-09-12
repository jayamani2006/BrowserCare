using System.Collections.Generic;

namespace BrowserCare.Core
{
    /// <summary>
    /// Central authority on what is Safe / Review / Protected.
    /// UI buttons never delete arbitrary paths directly — everything routes through here.
    /// </summary>
    public static class SafetyEngine
    {
        // Well-known Chrome sub-folder names, keyed to their classification.
        // Matched case-insensitively against a profile's immediate children.
        private static readonly Dictionary<string, (SafetyLevel Level, string Description, string Consequences)> KnownFolders = new()
        {
            ["Cache"] = (SafetyLevel.Safe, "Temporary files that help pages load faster.", "None expected. Chrome rebuilds this automatically."),
            ["Code Cache"] = (SafetyLevel.Safe, "Compiled JavaScript/WASM cache.", "None expected. Chrome recreates it on demand."),
            ["GPUCache"] = (SafetyLevel.Safe, "Cached GPU shader/rendering data.", "None expected. Chrome regenerates it."),
            ["GrShaderCache"] = (SafetyLevel.Safe, "Compiled graphics shaders.", "None expected. Regenerated automatically."),
            ["ShaderCache"] = (SafetyLevel.Safe, "Compiled graphics shaders.", "None expected. Regenerated automatically."),
            ["Media Cache"] = (SafetyLevel.Safe, "Temporary cache for audio/video playback.", "None expected."),
            ["Application Cache"] = (SafetyLevel.Safe, "Legacy offline application cache.", "None expected."),
            ["Service Worker\\CacheStorage"] = (SafetyLevel.Safe, "Disposable cache used by websites' service workers.", "Chrome/websites can usually recreate this; some offline data may need to re-download."),
            ["Service Worker\\ScriptCache"] = (SafetyLevel.Safe, "Cached service worker script bytecode.", "None expected. Rebuilt automatically."),
            ["Component Cache"] = (SafetyLevel.Safe, "Downloaded Chrome component updates.", "Chrome re-downloads components when needed."),
            ["OptimizationGuidePredictionModels"] = (SafetyLevel.Safe, "On-device optimization/prediction models Chrome downloaded.", "Chrome re-downloads these models automatically."),

            ["IndexedDB"] = (SafetyLevel.Review, "Persistent app data websites store in your browser (e.g. offline data, drafts).", "Removing this may sign you out of some sites or reset offline/app data."),
            ["Local Storage"] = (SafetyLevel.Review, "Small persistent data websites store (settings, tokens).", "Removing this may sign you out of some sites."),
            ["Session Storage"] = (SafetyLevel.Review, "Per-tab temporary site data.", "Minimal impact — usually cleared when tabs close anyway."),
            ["File System"] = (SafetyLevel.Review, "Files websites have stored via browser file APIs.", "Removing this may delete web-app data such as drafts."),
            ["Storage"] = (SafetyLevel.Review, "Modern per-origin site storage (quota-managed).", "Removing this may reset website data or sign you out."),
            ["Favicons"] = (SafetyLevel.Review, "Cached site icons.", "Minimal impact — icons just reload next visit."),

            ["Cookies"] = (SafetyLevel.Protected, "Sign-in sessions and site preferences.", "Never removed by Safe Cleanup."),
            ["Login Data"] = (SafetyLevel.Protected, "Saved passwords.", "Never removed by Safe Cleanup."),
            ["Web Data"] = (SafetyLevel.Protected, "Autofill, payment, and search-engine data.", "Never removed by Safe Cleanup."),
            ["Bookmarks"] = (SafetyLevel.Protected, "Your saved bookmarks.", "Never removed by Safe Cleanup."),
            ["History"] = (SafetyLevel.Protected, "Your browsing history.", "Never removed by Safe Cleanup."),
            ["Preferences"] = (SafetyLevel.Protected, "Profile configuration and settings.", "Never removed by Safe Cleanup."),
            ["Extensions"] = (SafetyLevel.Protected, "Installed extension files.", "Removed only via explicit extension-removal action, never Safe Cleanup."),
        };

        public static (SafetyLevel Level, string Description, string Consequences) Classify(string folderRelativeName)
        {
            if (KnownFolders.TryGetValue(folderRelativeName, out var known))
                return known;

            // Unknown item — default to Review so nothing unrecognized is auto-deleted.
            return (SafetyLevel.Review,
                    "Unrecognized Chrome data. BrowserCare doesn't have a specific explanation for this item yet.",
                    "Effect unknown — reviewed manually before any cleanup.");
        }

        public static bool IsCleanableBySafeCleanup(SafetyLevel level) => level == SafetyLevel.Safe;
    }
}
