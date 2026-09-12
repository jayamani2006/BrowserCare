using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BrowserCare.Chrome;
using BrowserCare.Cleanup;
using BrowserCare.Core;
using BrowserCare.Reports;

namespace BrowserCare
{
    public partial class MainWindow : Window
    {
        private readonly ScanEngine _scanEngine = new();
        private readonly ExtensionAnalyzer _extensionAnalyzer = new();
        private readonly ChromeDatabaseReader _dbReader = new();
        private readonly CacheCleaner _cacheCleaner = new();
        private readonly ReportGenerator _reportGenerator = new();
        private readonly ChromeDetector _chromeDetector = new();

        private CancellationTokenSource? _scanCts;
        private ScanResult? _lastScan;
        private string? _lastReportPath;

        public MainWindow()
        {
            InitializeComponent();
            LoadSettingsIntoUi();
            LoadSystemInfo();
            HighlightNav("Dashboard"); // otherwise the active page has zero visual indication on first load
        }

        // ===================== SYSTEM INFO (Dashboard header) =====================

        private void LoadSystemInfo()
        {
            PcNameText.Text = Environment.MachineName;
            SettingsPcNameText.Text = Environment.MachineName;

            var userDataPath = _chromeDetector.FindUserDataDirectory();
            ChromePathText.Text = userDataPath ?? "Not found";
            SettingsChromePathText.Text = userDataPath ?? "Not found";
        }

        // Fullscreen toggle removed — WPF's native window chrome already provides
        // minimize/maximize/restore in the title bar. A custom in-app button for
        // something the OS already gives you for free was redundant.

        // ===================== NAVIGATION =====================

        private void NavItem_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not TextBlock tb || tb.Tag is not string page)
                return;
            NavigateTo(page);
        }

        private void NavigateTo(string page)
        {
            foreach (var grid in AllPages())
                grid.Visibility = Visibility.Collapsed;

            var target = PageByName(page);
            if (target != null)
                target.Visibility = Visibility.Visible;

            HighlightNav(page);

            // Refresh page content from the last scan whenever the user navigates to it,
            // so pages never show stale data from a previous scan silently.
            switch (page)
            {
                case "Profiles": RenderProfilesPage(); break;
                case "Storage": RenderStoragePage(); break;
                case "Cleanup": RenderCleanupPage(); break;
                case "History": PopulateAccountSelector(HistoryAccountSelector); break;
                case "Downloads": PopulateAccountSelector(DownloadsAccountSelector); break;
                case "Extensions": PopulateAccountSelector(ExtensionsAccountSelector); break;
                case "Cookies": PopulateAccountSelector(CookiesAccountSelector); break;
            }
        }

        private void QuickAction_Cleanup(object sender, System.Windows.Input.MouseButtonEventArgs e) => NavigateTo("Cleanup");
        private void QuickAction_History(object sender, System.Windows.Input.MouseButtonEventArgs e) => NavigateTo("History");
        private void QuickAction_Reports(object sender, System.Windows.Input.MouseButtonEventArgs e) => NavigateTo("Reports");

        // ===================== ACCOUNT SCOPING (History / Downloads / Extensions / Cookies) =====================

        private sealed class ProfileOption
        {
            public required string Label { get; init; }
            /// <summary>Null means "All Accounts" — every profile.</summary>
            public string? FolderName { get; init; }
        }

        /// <summary>
        /// Fills an account-selector dropdown with "All Accounts" plus every real
        /// detected profile, labeled with its actual account — never just a bare
        /// "Profile 27" folder name, which gives the user nothing to decide from.
        /// Listing profiles is a cheap directory read, not a deep scan, so this is
        /// safe to do automatically on navigation rather than requiring a button.
        /// </summary>
        private void PopulateAccountSelector(ComboBox selector)
        {
            var previousSelection = (selector.SelectedItem as ProfileOption)?.FolderName;

            var profiles = CurrentProfiles();
            var options = new List<ProfileOption> { new ProfileOption { Label = "All Accounts", FolderName = null } };
            options.AddRange(profiles.Select(p => new ProfileOption
            {
                Label = $"{p.DisplayName} · {p.AccountLabel}",
                FolderName = p.FolderName
            }));

            selector.ItemsSource = options;
            selector.SelectedIndex = previousSelection == null
                ? 0
                : Math.Max(0, options.FindIndex(o => o.FolderName == previousSelection));
        }

        /// <summary>Resolves which real profiles the given selector currently scopes to.</summary>
        private List<ChromeProfile> ResolveSelectedProfiles(ComboBox selector)
        {
            var all = CurrentProfiles();
            if (selector.SelectedItem is ProfileOption { FolderName: not null } option)
                return all.Where(p => p.FolderName == option.FolderName).ToList();
            return all;
        }

        private void HistoryAccountSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Re-run whatever's currently on screen against the newly selected account.
            var currentFilter = HistoryFilterBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(currentFilter))
                RenderHistoryFiltered(currentFilter);
            else
                _ = RenderHistoryOverview();
        }

        private IEnumerable<Grid> AllPages()
        {
            yield return Page_Dashboard;
            yield return Page_Profiles;
            yield return Page_Storage;
            yield return Page_History;
            yield return Page_Downloads;
            yield return Page_Extensions;
            yield return Page_Cookies;
            yield return Page_Cleanup;
            yield return Page_Reports;
            yield return Page_Help;
            yield return Page_Settings;
            yield return Page_Privacy;
            yield return Page_About;
        }

        private Grid? PageByName(string name) => name switch
        {
            "Dashboard" => Page_Dashboard,
            "Profiles" => Page_Profiles,
            "Storage" => Page_Storage,
            "History" => Page_History,
            "Downloads" => Page_Downloads,
            "Extensions" => Page_Extensions,
            "Cookies" => Page_Cookies,
            "Cleanup" => Page_Cleanup,
            "Reports" => Page_Reports,
            "Help" => Page_Help,
            "Settings" => Page_Settings,
            "Privacy" => Page_Privacy,
            "About" => Page_About,
            _ => null
        };

        private void HighlightNav(string activePage)
        {
            var silver = (Brush)FindResource("Silver");
            var secondary = (Brush)FindResource("TextSecondary");
            var cardHover = (Brush)FindResource("CardHover");
            var navItems = new (string Tag, TextBlock Block)[]
            {
                ("Dashboard", Nav_Dashboard), ("Profiles", Nav_Profiles), ("Storage", Nav_Storage),
                ("History", Nav_History), ("Downloads", Nav_Downloads), ("Extensions", Nav_Extensions),
                ("Cookies", Nav_Cookies), ("Cleanup", Nav_Cleanup), ("Reports", Nav_Reports), ("Help", Nav_Help),
                ("Settings", Nav_Settings), ("Privacy", Nav_Privacy), ("About", Nav_About)
            };
            foreach (var (tag, block) in navItems)
            {
                var isActive = tag == activePage;
                block.Foreground = isActive ? silver : secondary;
                block.FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal;
                block.Background = isActive ? cardHover : Brushes.Transparent;
            }
        }

        // ===================== DASHBOARD / SCAN =====================

        private async void QuickScan_Click(object sender, RoutedEventArgs e) => await RunScanAsync(ScanMode.Quick);

        private async void DeepScan_Click(object sender, RoutedEventArgs e) => await RunScanAsync(ScanMode.Deep);

        private void CancelScan_Click(object sender, RoutedEventArgs e) => _scanCts?.Cancel();

        private async Task RunScanAsync(ScanMode mode)
        {
            _scanCts = new CancellationTokenSource();
            CancelScanButton.IsEnabled = true;
            ScanProgressBar.Visibility = Visibility.Visible;

            var progress = new Progress<string>(message => StatusText.Text = message);

            try
            {
                var result = await _scanEngine.ScanAsync(mode, progress, _scanCts.Token);
                _lastScan = result;
                RenderResult(result);
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = "Scan cancelled.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Scan failed: {ex.Message}";
            }
            finally
            {
                CancelScanButton.IsEnabled = false;
                ScanProgressBar.Visibility = Visibility.Collapsed;
                _scanCts?.Dispose();
                _scanCts = null;
            }
        }

        private void RenderResult(ScanResult result)
        {
            if (!result.ChromeDetected)
            {
                TotalSizeText.Text = "—";
                StatusText.Text = "Google Chrome wasn't found on this computer.";
                return;
            }

            TotalSizeText.Text = result.TotalSizeDisplay;
            SafeSizeText.Text = ScanItem.FormatBytes(result.SafeCleanupBytes);
            ReviewSizeText.Text = ScanItem.FormatBytes(result.ReviewBytes);
            ProtectedSizeText.Text = ScanItem.FormatBytes(result.ProtectedBytes);
            ProfileCountText.Text = result.Profiles.Count.ToString();

            var skippedNote = result.SkippedItems.Count > 0
                ? $" ({result.SkippedItems.Count} locked items skipped)"
                : string.Empty;
            StatusText.Text = $"Scan complete — {result.Profiles.Count} profile(s) analyzed.{skippedNote}";

            ProfilesList.ItemsSource = result.Profiles;

            // Size the segmented storage bar proportionally to real Safe/Review/Protected
            // totals. GridLength(0) columns collapse to nothing, so give each a small
            // floor so a present-but-tiny category still shows a sliver, not nothing.
            double safe = Math.Max(result.SafeCleanupBytes, result.TotalSizeBytes > 0 ? result.TotalSizeBytes * 0.01 : 0);
            double review = Math.Max(result.ReviewBytes, result.TotalSizeBytes > 0 ? result.TotalSizeBytes * 0.01 : 0);
            double prot = Math.Max(result.ProtectedBytes, result.TotalSizeBytes > 0 ? result.TotalSizeBytes * 0.01 : 0);
            SegSafe.Width = new GridLength(safe, GridUnitType.Star);
            SegReview.Width = new GridLength(review, GridUnitType.Star);
            SegProtected.Width = new GridLength(prot, GridUnitType.Star);
        }

        // ===================== PROFILES PAGE =====================

        private void RenderProfilesPage()
        {
            if (_lastScan == null || !_lastScan.ChromeDetected)
            {
                ProfilesPageEmptyText.Visibility = Visibility.Visible;
                ProfilesDetailList.ItemsSource = null;
                return;
            }
            ProfilesPageEmptyText.Visibility = Visibility.Collapsed;
            ProfilesDetailList.ItemsSource = _lastScan.Profiles;
        }

        // ===================== STORAGE PAGE =====================

        private sealed class CategoryTotal : System.ComponentModel.INotifyPropertyChanged
        {
            public required string Name { get; init; }
            public long SizeBytes { get; set; }
            public SafetyLevel Safety { get; set; }
            public bool CanClean => Safety == SafetyLevel.Safe;
            public string SizeDisplay => ScanItem.FormatBytes(SizeBytes);
            public List<ScanItem> Items { get; } = new();
            public List<ProfileShare> ProfileShares { get; set; } = new();

            private bool _isExpanded;
            public bool IsExpanded
            {
                get => _isExpanded;
                set { _isExpanded = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsExpanded))); }
            }

            public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        }

        private sealed class ProfileShare
        {
            public required string Label { get; init; }
            public long SizeBytes { get; init; }
            public string SizeDisplay => ScanItem.FormatBytes(SizeBytes);
        }

        private void RenderStoragePage()
        {
            if (_lastScan == null || !_lastScan.ChromeDetected)
            {
                StoragePageEmptyText.Visibility = Visibility.Visible;
                StorageCategoryList.ItemsSource = null;
                return;
            }
            StoragePageEmptyText.Visibility = Visibility.Collapsed;

            // "Profile 27" tells the user nothing they can act on — always pair it
            // with the real account label so a decision is actually possible.
            var accountLabels = _lastScan.Profiles.ToDictionary(
                p => p.FolderName,
                p => $"{p.DisplayName} · {p.AccountLabel}");

            var totals = new Dictionary<string, CategoryTotal>();
            foreach (var profile in _lastScan.Profiles)
            {
                foreach (var item in profile.Items)
                {
                    if (!totals.TryGetValue(item.Name, out var existing))
                    {
                        existing = new CategoryTotal { Name = item.Name, Safety = item.Safety };
                        totals[item.Name] = existing;
                    }
                    existing.SizeBytes += item.SizeBytes;
                    existing.Items.Add(item);
                }
            }

            foreach (var category in totals.Values)
            {
                category.ProfileShares = category.Items
                    .GroupBy(i => i.ProfileId ?? "(unknown)")
                    .Select(g => new ProfileShare
                    {
                        Label = accountLabels.TryGetValue(g.Key, out var label) ? label : g.Key,
                        SizeBytes = g.Sum(i => i.SizeBytes)
                    })
                    .OrderByDescending(p => p.SizeBytes)
                    .ToList();
            }

            StorageCategoryList.ItemsSource = totals.Values.OrderByDescending(c => c.SizeBytes).ToList();
        }

        private void ToggleCategoryDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is CategoryTotal category)
                category.IsExpanded = !category.IsExpanded;
        }

        private void CleanCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not CategoryTotal category) return;
            if (!category.CanClean) return;

            var confirm = MessageBox.Show(
                $"Clean \"{category.Name}\" across all profiles ({category.SizeDisplay})?",
                "Clean Category", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            var result = _cacheCleaner.CleanSafeItems(category.Items);
            MessageBox.Show($"Recovered {ScanItem.FormatBytes(result.RecoveredBytes)} from {category.Name}.",
                "Clean Category", MessageBoxButton.OK, MessageBoxImage.Information);
            RenderStoragePage();
        }

        // ===================== HISTORY PAGE =====================

        private List<ChromeProfile> CurrentProfiles()
        {
            var userDataPath = _chromeDetector.FindUserDataDirectory();
            return userDataPath == null ? new List<ChromeProfile>() : new ProfileScanner().DiscoverProfiles(userDataPath);
        }

        private void AnalyzeHistory_Click(object sender, RoutedEventArgs e)
        {
            // Defensive: make sure the selector is populated even if navigation
            // somehow didn't trigger it, so this never silently does nothing.
            if (HistoryAccountSelector.ItemsSource == null)
                PopulateAccountSelector(HistoryAccountSelector);
            _ = RenderHistoryOverview();
        }

        private async Task RenderHistoryOverview()
        {
            try
            {
                await RenderHistoryOverviewCore();
            }
            catch (Exception ex)
            {
                HistoryResultsPanel.Children.Clear();
                HistoryResultsPanel.Children.Add(InfoText($"History analysis failed: {ex.Message}"));
            }
            finally
            {
                HistoryProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private async Task RenderHistoryOverviewCore()
        {
            var profiles = ResolveSelectedProfiles(HistoryAccountSelector);
            HistoryResultsPanel.Children.Clear();

            if (profiles.Count == 0)
            {
                HistoryResultsPanel.Children.Add(InfoText("Google Chrome wasn't found on this computer."));
                return;
            }

            HistoryProgressBar.Visibility = Visibility.Visible;

            // The actual SQLite reads happen off the UI thread so the progress
            // bar has a chance to actually paint before the work finishes —
            // doing this synchronously on the UI thread never lets it render.
            var (grandTotal, domainTotals, oldest, newest, unavailableCount) = await Task.Run(() =>
            {
                long total = 0;
                var domains = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                DateTime? oldestLocal = null, newestLocal = null;
                int unavailable = 0;

                foreach (var profile in profiles)
                {
                    var summary = _dbReader.ReadHistorySummary(profile.FullPath);
                    if (!summary.Available) { unavailable++; continue; }

                    total += summary.TotalEntries;
                    foreach (var (domain, count) in summary.TopDomains)
                    {
                        domains.TryGetValue(domain, out var existing);
                        domains[domain] = existing + count;
                    }
                    if (summary.OldestVisitUtc.HasValue && (oldestLocal == null || summary.OldestVisitUtc < oldestLocal))
                        oldestLocal = summary.OldestVisitUtc;
                    if (summary.NewestVisitUtc.HasValue && (newestLocal == null || summary.NewestVisitUtc > newestLocal))
                        newestLocal = summary.NewestVisitUtc;
                }
                return (total, domains, oldestLocal, newestLocal, unavailable);
            });

            var scopeLabel = (HistoryAccountSelector.SelectedItem as ProfileOption)?.FolderName != null
                ? (HistoryAccountSelector.SelectedItem as ProfileOption)!.Label
                : $"{profiles.Count} account(s)";
            HistoryResultsPanel.Children.Add(InfoText($"Total history entries — {scopeLabel}: {grandTotal:N0}"));
            if (oldest.HasValue && newest.HasValue)
                HistoryResultsPanel.Children.Add(InfoText($"Date range: {oldest.Value.ToLocalTime():d MMM yyyy} — {newest.Value.ToLocalTime():d MMM yyyy}"));
            if (unavailableCount > 0)
                HistoryResultsPanel.Children.Add(InfoText($"{unavailableCount} profile(s) had no readable History database."));

            if (domainTotals.Count > 0)
            {
                HistoryResultsPanel.Children.Add(SectionTitle("Most-visited sites"));
                foreach (var kv in domainTotals.OrderByDescending(k => k.Value).Take(10))
                    HistoryResultsPanel.Children.Add(InfoText($"{kv.Key} — {kv.Value:N0} visits"));
            }
        }

        private int _historyDateRangeDays = 0; // 0 = all time
        private HistorySortOrder _historySortOrder = HistorySortOrder.MostVisited;

        private void HistoryFilterBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                RenderHistoryFiltered(HistoryFilterBox.Text.Trim());
        }

        private void HistoryDateFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string tagValue) return;
            _historyDateRangeDays = int.Parse(tagValue);
            HighlightToggleGroup(btn, DateFilter_Today, DateFilter_7d, DateFilter_30d, DateFilter_All);
            RefreshHistoryView();
        }

        private void HistorySortMostVisited_Click(object sender, RoutedEventArgs e)
        {
            _historySortOrder = HistorySortOrder.MostVisited;
            HighlightToggleGroup(SortFilter_Visited, SortFilter_Visited, SortFilter_Recent);
            RefreshHistoryView();
        }

        private void HistorySortMostRecent_Click(object sender, RoutedEventArgs e)
        {
            _historySortOrder = HistorySortOrder.MostRecent;
            HighlightToggleGroup(SortFilter_Recent, SortFilter_Visited, SortFilter_Recent);
            RefreshHistoryView();
        }

        /// <summary>Marks one button in a related group as the active choice (Silver text vs the default grey).</summary>
        private void HighlightToggleGroup(Button active, params Button[] group)
        {
            foreach (var b in group)
                b.Foreground = b == active ? (Brush)FindResource("FogWhite") : (Brush)FindResource("TextSecondary");
        }

        private void RefreshHistoryView()
        {
            var currentFilter = HistoryFilterBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(currentFilter))
                RenderHistoryFiltered(currentFilter);
        }

        private void RenderHistoryFiltered(string filterText)
        {
            _ = RenderHistoryFilteredAsync(filterText);
        }

        private async Task RenderHistoryFilteredAsync(string filterText)
        {
            try
            {
                await RenderHistoryFilteredCore(filterText);
            }
            catch (Exception ex)
            {
                HistoryResultsPanel.Children.Clear();
                HistoryResultsPanel.Children.Add(InfoText($"History search failed: {ex.Message}"));
            }
            finally
            {
                HistoryProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private async Task RenderHistoryFilteredCore(string filterText)
        {
            HistoryResultsPanel.Children.Clear();
            if (string.IsNullOrWhiteSpace(filterText))
            {
                await RenderHistoryOverviewCore();
                return;
            }

            var profiles = ResolveSelectedProfiles(HistoryAccountSelector);
            if (profiles.Count == 0)
            {
                HistoryResultsPanel.Children.Add(InfoText("Google Chrome wasn't found on this computer."));
                return;
            }

            HistoryProgressBar.Visibility = Visibility.Visible;

            DateTime? sinceUtc = _historyDateRangeDays > 0
                ? DateTime.UtcNow.AddDays(-_historyDateRangeDays)
                : null;

            var sortOrder = _historySortOrder;
            var allMatches = await Task.Run(() =>
            {
                var matches = new List<HistoryEntry>();
                foreach (var profile in profiles)
                {
                    var found = _dbReader.FindHistoryEntries(profile.FullPath, filterText, sinceUtc: sinceUtc, sort: sortOrder);
                    foreach (var m in found) m.ProfileFolder = profile.FolderName;
                    matches.AddRange(found);
                }
                return sortOrder == HistorySortOrder.MostRecent
                    ? matches.OrderByDescending(m => m.LastVisitUtc).ToList()
                    : matches.OrderByDescending(m => m.VisitCount).ToList();
            });

            var rangeLabel = _historyDateRangeDays switch
            {
                1 => " · Today",
                7 => " · Last 7 days",
                30 => " · Last 30 days",
                _ => string.Empty
            };
            HistoryResultsPanel.Children.Add(SectionTitle($"\"{filterText}\" — {allMatches.Count} matching page(s){rangeLabel}"));

            var deleteRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 12) };
            var deleteBtn = new Button { Content = "Delete matching history", Style = (Style)FindResource("SecondaryButton") };
            deleteBtn.Click += (_, _) => DeleteHistoryForCurrentFilter(filterText);
            deleteRow.Children.Add(deleteBtn);
            HistoryResultsPanel.Children.Add(deleteRow);

            foreach (var entry in allMatches.Take(100))
                HistoryResultsPanel.Children.Add(BuildHistoryEntryCard(entry));
            if (allMatches.Count > 100)
                HistoryResultsPanel.Children.Add(InfoText($"…and {allMatches.Count - 100} more."));
        }

        /// <summary>
        /// One history result as a proper card: site name on top, the raw URL
        /// underneath (trimmed, its own line), and an Open button — instead of
        /// mashing title/visits/URL into a single wrapped line that visually
        /// runs into the next entry.
        /// </summary>
        private Border BuildHistoryEntryCard(HistoryEntry entry)
        {
            var title = string.IsNullOrWhiteSpace(entry.Title) ? DomainOf(entry.Url) : entry.Title;

            var outer = new StackPanel();
            var titleRow = new Grid();
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleText = new TextBlock
            {
                Text = title,
                Foreground = (Brush)FindResource("TextPrimary"),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(titleText, 0);

            var openBtn = new Button { Content = "Open", Style = (Style)FindResource("RowActionButton") };
            Grid.SetColumn(openBtn, 1);
            var url = entry.Url;
            openBtn.Click += (_, _) =>
            {
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
                catch { /* no default browser association — nothing more we can do */ }
            };

            titleRow.Children.Add(titleText);
            titleRow.Children.Add(openBtn);

            var urlText = new TextBlock
            {
                Text = entry.Url,
                Foreground = (Brush)FindResource("TextSecondary"),
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            var metaText = new TextBlock
            {
                Text = $"{entry.VisitCount} visit(s)",
                Foreground = (Brush)FindResource("TextSecondary"),
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0)
            };

            outer.Children.Add(titleRow);
            outer.Children.Add(urlText);
            outer.Children.Add(metaText);

            return new Border
            {
                Style = (Style)FindResource("ProfileCard"),
                Padding = new Thickness(12, 8, 12, 8),
                Child = outer
            };
        }

        private static string DomainOf(string url)
        {
            try { return new Uri(url).Host; } catch { return url; }
        }

        private void DeleteHistoryForCurrentFilter(string filterText)
        {
            if (!RequireChromeClosedForWrite("delete matching history entries")) return;

            var confirm = MessageBox.Show(
                $"Permanently delete every history entry matching \"{filterText}\"?\n\nThis cannot be undone.",
                "Delete History", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            var profiles = ResolveSelectedProfiles(HistoryAccountSelector);
            int totalDeleted = 0;
            var errors = new List<string>();
            foreach (var profile in profiles)
            {
                if (_dbReader.DeleteHistoryForFilter(profile.FullPath, filterText, out var deleted, out var error))
                    totalDeleted += deleted;
                else if (error != null)
                    errors.Add($"{profile.DisplayName}: {error}");
            }

            MessageBox.Show(errors.Count == 0
                ? $"Deleted {totalDeleted} history entrie(s)."
                : $"Deleted {totalDeleted} entrie(s). Some profiles failed:\n{string.Join("\n", errors)}",
                "Delete History", MessageBoxButton.OK, errors.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);

            RenderHistoryFiltered(filterText);
        }

        private void DeleteAllHistory_Click(object sender, RoutedEventArgs e)
        {
            if (!RequireChromeClosedForWrite("delete all history")) return;

            var scopeLabel = (HistoryAccountSelector.SelectedItem as ProfileOption)?.FolderName != null
                ? (HistoryAccountSelector.SelectedItem as ProfileOption)!.Label
                : "every Chrome account";

            var confirm = MessageBox.Show(
                $"Permanently delete ALL browsing history for {scopeLabel}?\n\nThis cannot be undone.",
                "Delete All History", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            var profiles = ResolveSelectedProfiles(HistoryAccountSelector);
            var errors = new List<string>();
            foreach (var profile in profiles)
            {
                if (!_dbReader.DeleteAllHistory(profile.FullPath, out var error) && error != null)
                    errors.Add($"{profile.DisplayName}: {error}");
            }

            MessageBox.Show(errors.Count == 0
                ? "All history deleted."
                : $"Completed with some failures:\n{string.Join("\n", errors)}",
                "Delete All History", MessageBoxButton.OK, errors.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);

            _ = RenderHistoryOverview();
        }

        /// <summary>
        /// Chrome locks its SQLite files for writing while it's open. Rather than
        /// silently failing or force-killing Chrome, BrowserCare blocks destructive
        /// database writes and tells the user to close Chrome first.
        /// </summary>
        private bool RequireChromeClosedForWrite(string actionDescription)
        {
            if (!_chromeDetector.IsChromeRunning())
                return true;

            MessageBox.Show(
                $"Chrome is currently running. Please close Chrome completely before you {actionDescription} — Chrome keeps this file locked while it's open.",
                "Close Chrome First", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        // ===================== DOWNLOADS PAGE =====================

        private async void AnalyzeDownloads_Click(object sender, RoutedEventArgs e)
        {
            var profiles = ResolveSelectedProfiles(DownloadsAccountSelector);
            DownloadsResultsPanel.Children.Clear();

            if (profiles.Count == 0)
            {
                DownloadsResultsPanel.Children.Add(InfoText("Google Chrome wasn't found on this computer."));
                return;
            }

            DownloadsProgressBar.Visibility = Visibility.Visible;
            List<DownloadEntry> allDownloads;
            try
            {
                allDownloads = await Task.Run(() =>
                {
                    var results = new List<DownloadEntry>();
                    foreach (var profile in profiles)
                    {
                        var records = _dbReader.ListDownloads(profile.FullPath);
                        foreach (var r in records) r.ProfileFolder = profile.FolderName;
                        results.AddRange(records);
                    }
                    return results;
                });
            }
            finally
            {
                DownloadsProgressBar.Visibility = Visibility.Collapsed;
            }

            DownloadsResultsPanel.Children.Add(SectionTitle("Chrome download records"));
            DownloadsResultsPanel.Children.Add(InfoText($"{allDownloads.Count:N0} entries across {profiles.Count} profile(s)."));

            foreach (var d in allDownloads.Take(150))
                DownloadsResultsPanel.Children.Add(BuildDownloadResultCard(d));
            if (allDownloads.Count > 150)
                DownloadsResultsPanel.Children.Add(InfoText($"…and {allDownloads.Count - 150} more."));

            // Actual Downloads folder — a separate, real filesystem check.
            var downloadsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            DownloadsResultsPanel.Children.Add(SectionTitle("Your Downloads folder"));
            if (Directory.Exists(downloadsFolder))
            {
                try
                {
                    var files = Directory.GetFiles(downloadsFolder, "*", SearchOption.TopDirectoryOnly);
                    long totalSize = 0;
                    foreach (var f in files)
                    {
                        try { totalSize += new FileInfo(f).Length; } catch { /* skip locked */ }
                    }
                    DownloadsResultsPanel.Children.Add(InfoText($"{files.Length} file(s), {ScanItem.FormatBytes(totalSize)}"));
                    DownloadsResultsPanel.Children.Add(InfoText(downloadsFolder));
                }
                catch (Exception ex)
                {
                    DownloadsResultsPanel.Children.Add(InfoText($"Could not read the Downloads folder: {ex.Message}"));
                }
            }
            else
            {
                DownloadsResultsPanel.Children.Add(InfoText("No Downloads folder found for this user."));
            }
        }

        private void DownloadSearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter) SearchDownloads_Click(sender, e);
        }

        /// <summary>Finds which accounts have a matching download, regardless of the account selector.</summary>
        private async void SearchDownloads_Click(object sender, RoutedEventArgs e)
        {
            var keyword = DownloadSearchBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(keyword)) return;

            var profiles = CurrentProfiles();
            DownloadsResultsPanel.Children.Clear();
            DownloadsResultsPanel.Children.Add(SectionTitle($"\"{keyword}\" — matching downloads"));

            DownloadsProgressBar.Visibility = Visibility.Visible;
            List<(ChromeProfile Profile, List<DownloadEntry> Matches)> grouped;
            try
            {
                grouped = await Task.Run(() =>
                {
                    var results = new List<(ChromeProfile, List<DownloadEntry>)>();
                    foreach (var profile in profiles)
                    {
                        var matches = _dbReader.ListDownloads(profile.FullPath)
                            .Where(d => Path.GetFileName(d.TargetPath).Contains(keyword, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        if (matches.Count == 0) continue;
                        foreach (var m in matches) m.ProfileFolder = profile.FolderName;
                        results.Add((profile, matches));
                    }
                    return results;
                });
            }
            finally
            {
                DownloadsProgressBar.Visibility = Visibility.Collapsed;
            }

            int matchCount = 0;
            foreach (var (profile, matches) in grouped)
            {
                DownloadsResultsPanel.Children.Add(new TextBlock
                {
                    Text = $"{profile.DisplayName}  ·  {profile.AccountLabel}",
                    Foreground = (Brush)FindResource("Silver"),
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 10, 0, 6)
                });

                foreach (var d in matches)
                {
                    matchCount++;
                    DownloadsResultsPanel.Children.Add(BuildDownloadResultCard(d));
                }
            }

            if (matchCount == 0)
                DownloadsResultsPanel.Children.Add(InfoText(
                    $"\"{keyword}\" — either moved, deleted, or the name changed. Can't find that file in any account's downloads."));
        }

        /// <summary>One download search result: filename, date, size, Open, and Delete — used by search results.</summary>
        private Border BuildDownloadResultCard(DownloadEntry d)
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });

            var nameText = new TextBlock
            {
                Text = Path.GetFileName(d.TargetPath),
                Foreground = (Brush)FindResource("TextPrimary"),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var dateText = new TextBlock
            {
                Text = d.DateDisplay,
                Foreground = (Brush)FindResource("TextSecondary"),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(dateText, 1);
            var sizeText = new TextBlock
            {
                Text = d.SizeDisplay,
                Foreground = (Brush)FindResource("TextSecondary"),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(sizeText, 2);

            var openBtn = new Button { Content = "Open", Style = (Style)FindResource("RowActionButton") };
            Grid.SetColumn(openBtn, 3);
            var targetPath = d.TargetPath;
            openBtn.Click += (_, _) =>
            {
                if (!File.Exists(targetPath))
                {
                    MessageBox.Show(
                        $"\"{Path.GetFileName(targetPath)}\" — either moved, deleted, or the name changed. Can't find that file in Downloads.",
                        "File Not Found", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Opening a downloaded file with ShellExecute runs it with its
                // default handler — for executables, that means running it.
                // Require an explicit confirmation for those extensions rather
                // than launching them on a single click.
                var extension = Path.GetExtension(targetPath).ToLowerInvariant();
                var executableExtensions = new[] { ".exe", ".bat", ".cmd", ".msi", ".scr", ".ps1", ".vbs", ".js", ".jar", ".com" };
                if (executableExtensions.Contains(extension))
                {
                    var confirm = MessageBox.Show(
                        $"\"{Path.GetFileName(targetPath)}\" is an executable file. Opening it will run the program.\n\n" +
                        "Only continue if you trust this file and know what it does.",
                        "Run Executable?", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (confirm != MessageBoxResult.Yes) return;
                }

                try { Process.Start(new ProcessStartInfo(targetPath) { UseShellExecute = true }); }
                catch (Exception ex) { MessageBox.Show($"Couldn't open the file: {ex.Message}", "Open", MessageBoxButton.OK, MessageBoxImage.Error); }
            };

            var delBtn = new Button { Content = "Delete", Style = (Style)FindResource("RowActionButton") };
            Grid.SetColumn(delBtn, 4);
            delBtn.Click += (_, _) => DeleteSingleDownload(d);

            row.Children.Add(nameText);
            row.Children.Add(dateText);
            row.Children.Add(sizeText);
            row.Children.Add(openBtn);
            row.Children.Add(delBtn);
            return new Border { Style = (Style)FindResource("ProfileCard"), Child = row };
        }

        private void DeleteSingleDownload(DownloadEntry entry)
        {
            if (!RequireChromeClosedForWrite("delete a download record")) return;

            var confirm = MessageBox.Show(
                $"Remove this download record ({Path.GetFileName(entry.TargetPath)})?\n\nThis only removes the entry from Chrome's download history — it does not delete the actual file.",
                "Delete Download Record", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            var userDataPath = _chromeDetector.FindUserDataDirectory();
            if (userDataPath == null) return;
            var profilePath = Path.Combine(userDataPath, entry.ProfileFolder);

            if (_dbReader.DeleteDownloadRecord(profilePath, entry.Id, out var error))
                AnalyzeDownloads_Click(this, new RoutedEventArgs()); // refresh
            else
                MessageBox.Show($"Couldn't delete this record: {error}", "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void ClearAllDownloadRecords_Click(object sender, RoutedEventArgs e)
        {
            if (!RequireChromeClosedForWrite("clear all download records")) return;

            var scopeLabel = (DownloadsAccountSelector.SelectedItem as ProfileOption)?.FolderName != null
                ? (DownloadsAccountSelector.SelectedItem as ProfileOption)!.Label
                : "every Chrome account";

            var confirm = MessageBox.Show(
                $"Remove ALL download records for {scopeLabel}?\n\nThis only clears Chrome's download history — actual downloaded files are never touched.",
                "Clear All Download Records", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            var profiles = ResolveSelectedProfiles(DownloadsAccountSelector);
            var errors = new List<string>();
            foreach (var profile in profiles)
            {
                if (!_dbReader.DeleteAllDownloadRecords(profile.FullPath, out var error) && error != null)
                    errors.Add($"{profile.DisplayName}: {error}");
            }

            MessageBox.Show(errors.Count == 0 ? "All download records cleared." : $"Completed with some failures:\n{string.Join("\n", errors)}",
                "Clear All Download Records", MessageBoxButton.OK, errors.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);

            AnalyzeDownloads_Click(sender, e);
        }

        // ===================== COOKIES PAGE =====================

        private sealed class AccountGroup<T>
        {
            public required string ProfileLabel { get; init; }
            public required List<T> Items { get; init; }
        }

        private async void ScanCookies_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var profiles = ResolveSelectedProfiles(CookiesAccountSelector);
                if (profiles.Count == 0)
                {
                    CookiesList.ItemsSource = null;
                    MessageBox.Show("Google Chrome wasn't found on this computer.", "Scan Cookies", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                CookiesProgressBar.Visibility = Visibility.Visible;
                List<AccountGroup<CookieDomainInfo>> groups;
                try
                {
                    groups = await Task.Run(() =>
                    {
                        var result = new List<AccountGroup<CookieDomainInfo>>();
                        foreach (var profile in profiles)
                        {
                            var domains = _dbReader.ListCookieDomains(profile.FullPath);
                            foreach (var d in domains) d.ProfileFolder = profile.FolderName;
                            if (domains.Count == 0) continue;
                            result.Add(new AccountGroup<CookieDomainInfo>
                            {
                                ProfileLabel = $"{profile.DisplayName}  ·  {profile.AccountLabel}",
                                Items = domains.OrderByDescending(d => d.CookieCount).ToList()
                            });
                        }
                        return result;
                    });
                }
                finally
                {
                    CookiesProgressBar.Visibility = Visibility.Collapsed;
                }
                CookiesList.ItemsSource = groups;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cookie scan failed: {ex.Message}", "Scan Cookies", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CookieSearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter) SearchCookies_Click(sender, e);
        }

        /// <summary>Finds which accounts have cookies for a given site, regardless of the account selector.</summary>
        private async void SearchCookies_Click(object sender, RoutedEventArgs e)
        {
            var keyword = CookieSearchBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(keyword)) return;

            try
            {
                var profiles = CurrentProfiles();
                CookiesProgressBar.Visibility = Visibility.Visible;
                List<AccountGroup<CookieDomainInfo>> groups;
                try
                {
                    groups = await Task.Run(() =>
                    {
                        var result = new List<AccountGroup<CookieDomainInfo>>();
                        foreach (var profile in profiles)
                        {
                            var domains = _dbReader.ListCookieDomains(profile.FullPath)
                                .Where(d => d.Domain.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                                .ToList();
                            foreach (var d in domains) d.ProfileFolder = profile.FolderName;
                            if (domains.Count == 0) continue;
                            result.Add(new AccountGroup<CookieDomainInfo>
                            {
                                ProfileLabel = $"{profile.DisplayName}  ·  {profile.AccountLabel}",
                                Items = domains.OrderByDescending(d => d.CookieCount).ToList()
                            });
                        }
                        return result;
                    });
                }
                finally
                {
                    CookiesProgressBar.Visibility = Visibility.Collapsed;
                }
                CookiesList.ItemsSource = groups;

                MessageBox.Show(groups.Count == 0
                    ? $"No cookies matching \"{keyword}\" in any account."
                    : $"Found matching cookies in {groups.Count} account(s).",
                    "Search Cookies", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Search failed: {ex.Message}", "Search Cookies", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteCookiesForDomain_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not CookieDomainInfo domainInfo) return;
            if (!RequireChromeClosedForWrite("delete cookies for a site")) return;

            var confirm = MessageBox.Show(
                $"Delete all cookies for \"{domainInfo.Domain}\"?\n\nThis will sign you out of that site next time you open Chrome. This cannot be undone.",
                "Delete Cookies", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                var userDataPath = _chromeDetector.FindUserDataDirectory();
                if (userDataPath == null) return;
                var profilePath = Path.Combine(userDataPath, domainInfo.ProfileFolder);

                if (_dbReader.DeleteCookiesForDomain(profilePath, domainInfo.Domain, out var error))
                    ScanCookies_Click(sender, e); // refresh
                else
                    MessageBox.Show($"Couldn't delete these cookies: {error}", "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Couldn't delete these cookies: {ex.Message}", "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===================== EXTENSIONS PAGE =====================

        private async void ScanExtensions_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button clickedButton) clickedButton.IsEnabled = false;
            ExtensionsProgressBar.Visibility = Visibility.Visible;
            try
            {
                var profiles = ResolveSelectedProfiles(ExtensionsAccountSelector);
                if (profiles.Count == 0)
                {
                    ExtensionsList.ItemsSource = null;
                    MessageBox.Show("Google Chrome wasn't found on this computer.", "Scan Extensions", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var groups = new List<AccountGroup<ExtensionInfo>>();
                foreach (var profile in profiles)
                {
                    // NOTE: this used to call a blocking .GetAwaiter().GetResult() internally,
                    // which deadlocked the UI thread and made this button appear to do nothing.
                    // ScanProfileAsync is properly awaited now.
                    var extensions = await _extensionAnalyzer.ScanProfileAsync(profile);
                    if (extensions.Count == 0) continue;
                    groups.Add(new AccountGroup<ExtensionInfo>
                    {
                        ProfileLabel = $"{profile.DisplayName}  ·  {profile.AccountLabel}",
                        Items = extensions.OrderByDescending(x => x.SizeBytes).ToList()
                    });
                }
                ExtensionsList.ItemsSource = groups;

                if (groups.Count == 0)
                    MessageBox.Show("No extensions found in any Chrome profile.", "Scan Extensions", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Extension scan failed: {ex.Message}", "Scan Extensions", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (sender is Button b) b.IsEnabled = true;
                ExtensionsProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private void ExtensionSearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter) SearchExtension_Click(sender, e);
        }

        /// <summary>
        /// Searches for an extension by name across EVERY account (not just the
        /// selected one — that's the point of this search: "which of my accounts
        /// has this installed?"), and groups the matches by account.
        /// </summary>
        private async void SearchExtension_Click(object sender, RoutedEventArgs e)
        {
            var keyword = ExtensionSearchBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(keyword)) return;

            ExtensionsProgressBar.Visibility = Visibility.Visible;
            try
            {
                var profiles = CurrentProfiles();
                var groups = new List<AccountGroup<ExtensionInfo>>();
                foreach (var profile in profiles)
                {
                    var extensions = await _extensionAnalyzer.ScanProfileAsync(profile);
                    var matches = extensions.Where(x => x.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (matches.Count == 0) continue;
                    groups.Add(new AccountGroup<ExtensionInfo>
                    {
                        ProfileLabel = $"{profile.DisplayName}  ·  {profile.AccountLabel}",
                        Items = matches
                    });
                }
                ExtensionsList.ItemsSource = groups;

                MessageBox.Show(groups.Count == 0
                    ? $"\"{keyword}\" isn't installed in any account."
                    : $"Found \"{keyword}\" in {groups.Count} account(s).",
                    "Search Extension", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Search failed: {ex.Message}", "Search Extension", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ExtensionsProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private void RemoveExtension_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not ExtensionInfo ext)
                return;

            var confirm = MessageBox.Show(
                $"Remove \"{ext.Name}\" ({ext.SizeDisplay}) from {ext.ProfileFolder} only?\n\nThis deletes the extension's files. It does not affect any other profile or account.",
                "Remove Extension",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                var userDataPath = _chromeDetector.FindUserDataDirectory();
                if (userDataPath == null) return;

                var extensionFolder = Path.Combine(userDataPath, ext.ProfileFolder, "Extensions", ext.ExtensionId);
                if (Directory.Exists(extensionFolder))
                    Directory.Delete(extensionFolder, recursive: true);

                ScanExtensions_Click(sender, e); // refresh
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Couldn't remove this extension: {ex.Message}\n\nIt may be in use by Chrome. Close Chrome and try again.",
                    "Remove Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===================== CLEANUP PAGE =====================

        private void RenderCleanupPage()
        {
            if (_lastScan == null || !_lastScan.ChromeDetected)
            {
                CleanupSummaryText.Text = "Run a scan from the Dashboard first.";
                SafeCleanupButton.IsEnabled = false;
                CleanupPreviewList.ItemsSource = null;
                return;
            }

            CleanupSummaryText.Text = $"You can safely remove {_lastScan.SafeCleanupDisplay}. " +
                                       $"Only items Chrome can recreate automatically are included — " +
                                       $"nothing in Review or Protected is ever touched here.";
            SafeCleanupButton.IsEnabled = _lastScan.SafeCleanupBytes > 0;

            // Detailed preview: exactly which categories will be cleaned and how much
            // each is worth, aggregated across every profile — not just a single total.
            var preview = new Dictionary<string, CategoryTotal>();
            foreach (var profile in _lastScan.Profiles)
            {
                foreach (var item in profile.Items.Where(i => i.Safety == SafetyLevel.Safe))
                {
                    if (!preview.TryGetValue(item.Name, out var existing))
                    {
                        existing = new CategoryTotal { Name = item.Name, Safety = item.Safety };
                        preview[item.Name] = existing;
                    }
                    existing.SizeBytes += item.SizeBytes;
                }
            }
            CleanupPreviewList.ItemsSource = preview.Values.OrderByDescending(c => c.SizeBytes).ToList();
        }

        private async void SafeCleanup_Click(object sender, RoutedEventArgs e)
        {
            if (_lastScan == null) return;

            if (_chromeDetector.IsChromeRunning())
            {
                var proceed = MessageBox.Show(
                    "Chrome is currently running. Some cache files may be locked and skipped.\n\nContinue anyway?",
                    "Chrome Is Running", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (proceed != MessageBoxResult.Yes)
                    return;
            }

            var settings = SettingsManager.Load();
            if (settings.RequireCleanupConfirmation)
            {
                var confirm = MessageBox.Show(
                    $"Clean {_lastScan.SafeCleanupDisplay} of Safe items now?\n\nThis only removes cache-type data Chrome recreates automatically.",
                    "Confirm Safe Cleanup", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes)
                    return;
            }

            var safeItems = _lastScan.Profiles.SelectMany(p => p.Items).Where(i => i.Safety == SafetyLevel.Safe).ToList();

            SafeCleanupButton.IsEnabled = false;
            CleanupProgressBar.Visibility = Visibility.Visible;
            CleanupResult result;
            try
            {
                result = await Task.Run(() => _cacheCleaner.CleanSafeItems(safeItems));
            }
            finally
            {
                CleanupProgressBar.Visibility = Visibility.Collapsed;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Recovered: {ScanItem.FormatBytes(result.RecoveredBytes)}");
            sb.AppendLine();
            foreach (var entry in result.Log)
            {
                var status = entry.Success ? $"OK  {ScanItem.FormatBytes(entry.SizeBytes)}" : $"SKIPPED — {entry.Error}";
                sb.AppendLine($"{entry.Item,-24} {status}");
            }
            CleanupLogText.Text = sb.ToString();

            CleanupSummaryText.Text = $"Cleanup complete. Recovered {ScanItem.FormatBytes(result.RecoveredBytes)}. " +
                                       "Run a new scan to see updated totals.";
            CleanupPreviewList.ItemsSource = null;
        }

        private void CleanSingleItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not ScanItem item) return;
            if (item.Safety != SafetyLevel.Safe) return;

            var result = _cacheCleaner.CleanSafeItems(new[] { item });
            var entry = result.Log.FirstOrDefault();
            var message = entry != null && entry.Success
                ? $"Cleaned {item.Name} — recovered {ScanItem.FormatBytes(entry.SizeBytes)}."
                : $"Couldn't clean {item.Name}: {entry?.Error ?? "unknown error"}";
            MessageBox.Show(message, "Clean Item", MessageBoxButton.OK,
                entry != null && entry.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        // ===================== REPORTS PAGE =====================

        private void GenerateReport_Click(object sender, RoutedEventArgs e)
        {
            if (_lastScan == null)
            {
                GeneratedReportText.Text = "Run a scan from the Dashboard first — there's nothing to report yet.";
                ReportStatusText.Text = "No scan data available yet.";
                return;
            }

            GeneratedReportText.Text = _reportGenerator.GenerateText(_lastScan);
            ReportStatusText.Text = "Report generated below. Click Download to save it as a text file.";
        }

        private void ExportReport_Click(object sender, RoutedEventArgs e)
        {
            if (_lastScan == null)
            {
                ReportStatusText.Text = "Run a scan from the Dashboard first — there's nothing to report yet.";
                return;
            }

            try
            {
                var reportsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "BrowserCare Reports");
                _lastReportPath = _reportGenerator.SaveToFile(_lastScan, reportsDir);
                ReportStatusText.Text = $"Report saved to:\n{_lastReportPath}";
                OpenReportFolderButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                ReportStatusText.Text = $"Couldn't save the report: {ex.Message}";
            }
        }

        private void OpenReportFolder_Click(object sender, RoutedEventArgs e)
        {
            if (_lastReportPath == null) return;
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_lastReportPath}\"") { UseShellExecute = true });
            }
            catch
            {
                // Non-fatal — the report still exists even if we can't open Explorer for the user.
            }
        }

        // ===================== SETTINGS PAGE =====================

        private void LoadSettingsIntoUi()
        {
            var settings = SettingsManager.Load();
            Setting_StartWithWindows.IsChecked = settings.StartWithWindows;
            Setting_RequireConfirmation.IsChecked = settings.RequireCleanupConfirmation;
            Setting_CreateBackups.IsChecked = settings.CreateBackupsBeforeReviewCleanup;
            Setting_IncludeInactive.IsChecked = settings.IncludeInactiveProfiles;
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            var settings = new AppSettings
            {
                StartWithWindows = Setting_StartWithWindows.IsChecked == true,
                RequireCleanupConfirmation = Setting_RequireConfirmation.IsChecked == true,
                CreateBackupsBeforeReviewCleanup = Setting_CreateBackups.IsChecked == true,
                IncludeInactiveProfiles = Setting_IncludeInactive.IsChecked == true
            };
            SettingsManager.Save(settings);
            SettingsSavedText.Text = "Settings saved.";
        }

        // ===================== ABOUT PAGE =====================

        private void ContactGeneral_Click(object sender, RoutedEventArgs e) => OpenMailTo("jssofttoolproducts@gmail.com", "BrowserCare");

        private void ContactSupport_Click(object sender, RoutedEventArgs e) => OpenMailTo("support.jssofttoolproducts@gmail.com", "BrowserCare Support");

        private static void OpenMailTo(string address, string subject)
        {
            try
            {
                var uri = $"mailto:{address}?subject={Uri.EscapeDataString(subject)}";
                Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
            }
            catch
            {
                // Non-fatal — if no default mail client is configured, there's nothing more to do here.
            }
        }

        // ===================== SCROLL SMOOTHING =====================

        /// <summary>
        /// WPF's default mouse-wheel scroll jumps by a large fixed amount per
        /// notch, which feels jerky in long lists (History, Extensions, etc.).
        /// This halves the effective step for a smoother, slower scroll.
        /// </summary>
        private void SmoothScroll_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer sv) return;
            e.Handled = true;
            sv.ScrollToVerticalOffset(sv.VerticalOffset - (e.Delta / 2.0));
        }

        // ===================== SHARED UI HELPERS =====================

        private TextBlock SectionTitle(string text) => new()
        {
            Text = text,
            Foreground = (Brush)FindResource("TextPrimary"),
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 14, 0, 4)
        };

        private TextBlock InfoText(string text) => new()
        {
            Text = text,
            Foreground = (Brush)FindResource("TextSecondary"),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 0)
        };
    }
}
