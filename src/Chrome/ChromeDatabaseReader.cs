using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace BrowserCare.Chrome
{
    public sealed class HistorySummary
    {
        public long TotalEntries { get; set; }
        public List<(string Domain, long VisitCount)> TopDomains { get; } = new();
        public DateTime? OldestVisitUtc { get; set; }
        public DateTime? NewestVisitUtc { get; set; }
        public bool Available { get; set; }
        public string? UnavailableReason { get; set; }
    }

    public sealed class DownloadsSummary
    {
        public long RecordCount { get; set; }
        public bool Available { get; set; }
        public string? UnavailableReason { get; set; }
    }

    public sealed class HistoryEntry
    {
        public long UrlId { get; set; }
        public required string Url { get; init; }
        public required string Title { get; init; }
        public long VisitCount { get; set; }
        public DateTime? LastVisitUtc { get; set; }
        public string ProfileFolder { get; set; } = string.Empty;
    }

    public enum HistorySortOrder { MostVisited, MostRecent }

    public sealed class DownloadEntry
    {
        public long Id { get; set; }
        public required string TargetPath { get; init; }
        public long TotalBytes { get; set; }
        public DateTime? StartTimeUtc { get; set; }
        public string ProfileFolder { get; set; } = string.Empty;
        public string SizeDisplay => Core.ScanItem.FormatBytes(TotalBytes);
        public string DateDisplay => StartTimeUtc.HasValue ? StartTimeUtc.Value.ToLocalTime().ToString("d MMM yyyy") : "Unknown date";
    }

    public sealed class CookieDomainInfo
    {
        public required string Domain { get; init; }
        public int CookieCount { get; set; }
        public string ProfileFolder { get; set; } = string.Empty;
        public string CookieCountDisplay => $"{CookieCount} cookie(s)";
    }

    /// <summary>
    /// Reads Chrome's SQLite databases safely: always copies the (possibly locked)
    /// database to a temp file first and opens the copy read-only, so BrowserCare
    /// never writes to or blocks on Chrome's live files. Never modifies History,
    /// Cookies, Login Data, or Web Data.
    /// </summary>
    public sealed class ChromeDatabaseReader
    {
        // Chrome stores timestamps as microseconds since 1601-01-01 (the Windows/WebKit epoch).
        private static readonly DateTime ChromeEpoch = new(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public HistorySummary ReadHistorySummary(string profilePath)
        {
            var summary = new HistorySummary();
            var historyDb = Path.Combine(profilePath, "History");
            if (!File.Exists(historyDb))
            {
                summary.UnavailableReason = "No History database found for this profile.";
                return summary;
            }

            string? tempCopy = null;
            try
            {
                tempCopy = CopyToTemp(historyDb);
                using var connection = new SqliteConnection($"Data Source={tempCopy};Mode=ReadOnly");
                connection.Open();

                using (var countCmd = connection.CreateCommand())
                {
                    countCmd.CommandText = "SELECT COUNT(*) FROM urls";
                    summary.TotalEntries = Convert.ToInt64(countCmd.ExecuteScalar() ?? 0L);
                }

                using (var domainCmd = connection.CreateCommand())
                {
                    // Extract a rough "domain" from the URL and rank by total visit count.
                    domainCmd.CommandText = @"
                        SELECT url, visit_count FROM urls
                        ORDER BY visit_count DESC
                        LIMIT 200";
                    using var reader = domainCmd.ExecuteReader();
                    var domainTotals = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                    while (reader.Read())
                    {
                        var url = reader.GetString(0);
                        var visits = reader.GetInt64(1);
                        var domain = ExtractDomain(url);
                        if (domain == null) continue;
                        domainTotals.TryGetValue(domain, out var existing);
                        domainTotals[domain] = existing + visits;
                    }
                    foreach (var kv in Top(domainTotals, 10))
                        summary.TopDomains.Add((kv.Key, kv.Value));
                }

                using (var rangeCmd = connection.CreateCommand())
                {
                    rangeCmd.CommandText = "SELECT MIN(last_visit_time), MAX(last_visit_time) FROM urls";
                    using var reader = rangeCmd.ExecuteReader();
                    if (reader.Read() && !reader.IsDBNull(0) && !reader.IsDBNull(1))
                    {
                        summary.OldestVisitUtc = ChromeTimeToUtc(reader.GetInt64(0));
                        summary.NewestVisitUtc = ChromeTimeToUtc(reader.GetInt64(1));
                    }
                }

                summary.Available = true;
            }
            catch (Exception ex)
            {
                summary.UnavailableReason = $"Database currently unavailable ({ex.GetType().Name}).";
            }
            finally
            {
                TryDeleteTemp(tempCopy);
            }

            return summary;
        }

        public DownloadsSummary ReadDownloadsSummary(string profilePath)
        {
            var summary = new DownloadsSummary();
            var historyDb = Path.Combine(profilePath, "History"); // Chrome stores the "downloads" table in History.db
            if (!File.Exists(historyDb))
            {
                summary.UnavailableReason = "No History database found for this profile.";
                return summary;
            }

            string? tempCopy = null;
            try
            {
                tempCopy = CopyToTemp(historyDb);
                using var connection = new SqliteConnection($"Data Source={tempCopy};Mode=ReadOnly");
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM downloads";
                summary.RecordCount = Convert.ToInt64(cmd.ExecuteScalar() ?? 0L);
                summary.Available = true;
            }
            catch (Exception ex)
            {
                summary.UnavailableReason = $"Database currently unavailable ({ex.GetType().Name}).";
            }
            finally
            {
                TryDeleteTemp(tempCopy);
            }

            return summary;
        }

        // ===================== FILTERED BROWSING (read-only, temp copy) =====================

        /// <summary>
        /// Lists history entries whose URL or title contains the given filter text.
        /// Optionally restricts to entries visited after <paramref name="sinceUtc"/>,
        /// and sorts by visit count or by most recent visit.
        /// </summary>
        public List<HistoryEntry> FindHistoryEntries(string profilePath, string filterText,
            int limit = 200, DateTime? sinceUtc = null, HistorySortOrder sort = HistorySortOrder.MostVisited)
        {
            var results = new List<HistoryEntry>();
            var historyDb = Path.Combine(profilePath, "History");
            if (!File.Exists(historyDb)) return results;

            string? tempCopy = null;
            try
            {
                tempCopy = CopyToTemp(historyDb);
                using var connection = new SqliteConnection($"Data Source={tempCopy};Mode=ReadOnly");
                connection.Open();

                var orderBy = sort == HistorySortOrder.MostRecent ? "last_visit_time DESC" : "visit_count DESC";
                var dateClause = sinceUtc.HasValue ? "AND last_visit_time >= @since" : string.Empty;

                using var cmd = connection.CreateCommand();
                cmd.CommandText = $@"
                    SELECT id, url, title, visit_count, last_visit_time FROM urls
                    WHERE (url LIKE @filter OR title LIKE @filter) {dateClause}
                    ORDER BY {orderBy}
                    LIMIT @limit";
                cmd.Parameters.AddWithValue("@filter", $"%{filterText}%");
                cmd.Parameters.AddWithValue("@limit", limit);
                if (sinceUtc.HasValue)
                    cmd.Parameters.AddWithValue("@since", UtcToChromeTime(sinceUtc.Value));

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    results.Add(new HistoryEntry
                    {
                        UrlId = reader.GetInt64(0),
                        Url = reader.GetString(1),
                        Title = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        VisitCount = reader.GetInt64(3),
                        LastVisitUtc = reader.IsDBNull(4) ? null : ChromeTimeToUtc(reader.GetInt64(4))
                    });
                }
            }
            catch
            {
                // Return whatever was gathered before the failure; caller shows an empty/partial list.
            }
            finally
            {
                TryDeleteTemp(tempCopy);
            }
            return results;
        }

        /// <summary>Lists real download records (read-only, temp copy).</summary>
        public List<DownloadEntry> ListDownloads(string profilePath, int limit = 500)
        {
            var results = new List<DownloadEntry>();
            var historyDb = Path.Combine(profilePath, "History");
            if (!File.Exists(historyDb)) return results;

            string? tempCopy = null;
            try
            {
                tempCopy = CopyToTemp(historyDb);
                using var connection = new SqliteConnection($"Data Source={tempCopy};Mode=ReadOnly");
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT id, target_path, total_bytes, start_time FROM downloads ORDER BY id DESC LIMIT @limit";
                cmd.Parameters.AddWithValue("@limit", limit);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    results.Add(new DownloadEntry
                    {
                        Id = reader.GetInt64(0),
                        TargetPath = reader.IsDBNull(1) ? "(unknown)" : reader.GetString(1),
                        TotalBytes = reader.IsDBNull(2) ? 0 : reader.GetInt64(2),
                        StartTimeUtc = reader.IsDBNull(3) ? null : ChromeTimeToUtc(reader.GetInt64(3))
                    });
                }
            }
            catch
            {
                // Return whatever was gathered.
            }
            finally
            {
                TryDeleteTemp(tempCopy);
            }
            return results;
        }

        /// <summary>Lists distinct cookie domains and per-domain counts (read-only, temp copy).</summary>
        public List<CookieDomainInfo> ListCookieDomains(string profilePath, int limit = 300)
        {
            var results = new List<CookieDomainInfo>();
            var cookiesDb = Path.Combine(profilePath, "Network", "Cookies");
            if (!File.Exists(cookiesDb))
                cookiesDb = Path.Combine(profilePath, "Cookies"); // older Chrome versions
            if (!File.Exists(cookiesDb)) return results;

            string? tempCopy = null;
            try
            {
                tempCopy = CopyToTemp(cookiesDb);
                using var connection = new SqliteConnection($"Data Source={tempCopy};Mode=ReadOnly");
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT host_key, COUNT(*) FROM cookies
                    GROUP BY host_key
                    ORDER BY COUNT(*) DESC
                    LIMIT @limit";
                cmd.Parameters.AddWithValue("@limit", limit);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    results.Add(new CookieDomainInfo
                    {
                        Domain = reader.GetString(0),
                        CookieCount = reader.GetInt32(1)
                    });
                }
            }
            catch
            {
                // Return whatever was gathered.
            }
            finally
            {
                TryDeleteTemp(tempCopy);
            }
            return results;
        }

        // ===================== DESTRUCTIVE ACTIONS (direct write — Chrome must be closed) =====================
        // These operate on the REAL database file, never a temp copy — a delete against a
        // copy would silently do nothing. Callers must verify Chrome is closed first;
        // these methods still fail safely (return false) if the file is locked.

        public bool DeleteAllHistory(string profilePath, out string? error)
        {
            error = null;
            var historyDb = Path.Combine(profilePath, "History");
            if (!File.Exists(historyDb)) { error = "No History database found."; return false; }
            try
            {
                using var connection = new SqliteConnection($"Data Source={historyDb}");
                connection.Open();
                using var tx = connection.BeginTransaction();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = "DELETE FROM visits; DELETE FROM urls; DELETE FROM keyword_search_terms;";
                    cmd.ExecuteNonQuery();
                }
                tx.Commit();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public bool DeleteHistoryForFilter(string profilePath, string filterText, out int deletedCount, out string? error)
        {
            deletedCount = 0;
            error = null;
            var historyDb = Path.Combine(profilePath, "History");
            if (!File.Exists(historyDb)) { error = "No History database found."; return false; }
            try
            {
                using var connection = new SqliteConnection($"Data Source={historyDb}");
                connection.Open();
                using var tx = connection.BeginTransaction();

                using (var findCmd = connection.CreateCommand())
                {
                    findCmd.Transaction = tx;
                    findCmd.CommandText = "SELECT id FROM urls WHERE url LIKE @filter OR title LIKE @filter";
                    findCmd.Parameters.AddWithValue("@filter", $"%{filterText}%");
                    var ids = new List<long>();
                    using (var reader = findCmd.ExecuteReader())
                        while (reader.Read()) ids.Add(reader.GetInt64(0));

                    foreach (var id in ids)
                    {
                        using var delVisits = connection.CreateCommand();
                        delVisits.Transaction = tx;
                        delVisits.CommandText = "DELETE FROM visits WHERE url = @id";
                        delVisits.Parameters.AddWithValue("@id", id);
                        delVisits.ExecuteNonQuery();

                        using var delUrl = connection.CreateCommand();
                        delUrl.Transaction = tx;
                        delUrl.CommandText = "DELETE FROM urls WHERE id = @id";
                        delUrl.Parameters.AddWithValue("@id", id);
                        delUrl.ExecuteNonQuery();
                    }
                    deletedCount = ids.Count;
                }
                tx.Commit();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public bool DeleteDownloadRecord(string profilePath, long downloadId, out string? error)
        {
            error = null;
            var historyDb = Path.Combine(profilePath, "History");
            if (!File.Exists(historyDb)) { error = "No History database found."; return false; }
            try
            {
                using var connection = new SqliteConnection($"Data Source={historyDb}");
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "DELETE FROM downloads_url_chains WHERE id = @id; DELETE FROM downloads WHERE id = @id;";
                cmd.Parameters.AddWithValue("@id", downloadId);
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public bool DeleteAllDownloadRecords(string profilePath, out string? error)
        {
            error = null;
            var historyDb = Path.Combine(profilePath, "History");
            if (!File.Exists(historyDb)) { error = "No History database found."; return false; }
            try
            {
                using var connection = new SqliteConnection($"Data Source={historyDb}");
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "DELETE FROM downloads_url_chains; DELETE FROM downloads;";
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public bool DeleteCookiesForDomain(string profilePath, string domain, out string? error)
        {
            error = null;
            var cookiesDb = Path.Combine(profilePath, "Network", "Cookies");
            if (!File.Exists(cookiesDb))
                cookiesDb = Path.Combine(profilePath, "Cookies");
            if (!File.Exists(cookiesDb)) { error = "No Cookies database found."; return false; }
            try
            {
                using var connection = new SqliteConnection($"Data Source={cookiesDb}");
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "DELETE FROM cookies WHERE host_key = @domain";
                cmd.Parameters.AddWithValue("@domain", domain);
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static string CopyToTemp(string sourceDb)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"browsercare_{Guid.NewGuid():N}.sqlite");
            // Share ReadWrite so we can copy even while Chrome holds the file open.
            using (var src = new FileStream(sourceDb, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var dst = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
            {
                src.CopyTo(dst);
            }
            return tempPath;
        }

        private static void TryDeleteTemp(string? path)
        {
            if (path == null) return;
            try { File.Delete(path); } catch { /* best effort */ }
        }

        private static DateTime ChromeTimeToUtc(long chromeMicroseconds)
        {
            if (chromeMicroseconds <= 0) return ChromeEpoch;
            return ChromeEpoch.AddTicks(chromeMicroseconds * 10); // 1 microsecond = 10 ticks
        }

        private static long UtcToChromeTime(DateTime utc)
        {
            var ticksSinceEpoch = utc.ToUniversalTime().Ticks - ChromeEpoch.Ticks;
            return ticksSinceEpoch / 10; // 10 ticks = 1 microsecond
        }

        private static string? ExtractDomain(string url)
        {
            try
            {
                var uri = new Uri(url);
                return uri.Host;
            }
            catch
            {
                return null;
            }
        }

        private static IEnumerable<KeyValuePair<string, long>> Top(Dictionary<string, long> source, int count)
        {
            var list = new List<KeyValuePair<string, long>>(source);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            for (int i = 0; i < list.Count && i < count; i++)
                yield return list[i];
        }
    }
}
