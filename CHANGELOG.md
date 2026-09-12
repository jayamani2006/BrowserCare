# Changelog — BrowserCare™

All notable changes to BrowserCare are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.0.0] - 2026-09-12

### Initial Production Release

#### Core Architecture
- **Automatic Chrome Detection:** Dynamically resolves `%LOCALAPPDATA%\Google\Chrome\User Data` without hardcoding Windows usernames. Also supports Chrome Beta, Dev, and Canary channels.
- **Profile Discovery:** Discovers real profile folders and parses `Preferences` and `Local State` JSON manifests to present user display names and account emails.
- **3-Tier Safety Engine:** Enforces strict item classification (`🟢 Safe`, `🟡 Review`, `🔴 Protected`). Safe Cleanup is physically restricted to Safe items only.
- **Lockless SQLite Snapshots:** Clones History, Downloads, and Cookies databases to isolated temporary files in `%TEMP%` for read-only querying, eliminating file lock collisions.
- **Crash Logging & Temp Cleanup:** Hooks unhandled exceptions, writes diagnostics to `%LOCALAPPDATA%\BrowserCare\crash.log` (capped at 1 MB), and purges orphaned temp files upon application startup.

#### User Interface (13 Dedicated Views)
- **Dashboard:** Storage total counter, segmented Safe/Review/Protected visual bar, PC identification, Quick Actions rail, and real-time progress indicator.
- **Profiles:** Multi-account profile breakdown with per-category size metrics and clean triggers.
- **Storage:** Aggregated storage categories across all profiles with expandable detail drawers.
- **History:** Searchable history table with domain/keyword filtering, date chips (Today, 7d, 30d, All Time), and sorting options.
- **Downloads:** Chrome download history viewer with separate Downloads folder inspection and executable launch protection.
- **Extensions:** Manifest analyzer displaying installed extensions, version strings, disk footprint, and safe removal triggers.
- **Cookies:** Domain-level cookie count inspector with isolated per-domain deletion triggers.
- **Cleanup:** Preview of Safe items to be purged, space estimation, and live monospace execution log.
- **Reports:** Inline audit report generation and `.txt` file export.
- **Help:** In-app operational reference manual and troubleshooting guide.
- **Settings:** Persistent JSON configuration (`%LOCALAPPDATA%\BrowserCare\settings.json`) managing startup behavior and safety prompts.
- **Privacy:** Comprehensive local execution and zero-telemetry disclosure.
- **About:** Brand identity (Chip-X / JS SoftTools), developer credits (Jayasubramani S), support triggers, and legal disclaimers.

#### Packaging & Deployment
- Self-contained single-file compilation for both 64-bit (`win-x64`) and 32-bit (`win-x86`) Windows.
- Single unified Inno Setup installer (`BrowserCare-Setup-1.0.0.exe`) with architecture auto-detection.
