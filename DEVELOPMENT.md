# BrowserCare — Development Guide

## Requirements

- **Windows 10/11 x64** (development and end-user)
- **.NET 8 SDK** — https://dotnet.microsoft.com/download/dotnet/8.0
  - Verify: `dotnet --version` should print `8.x.x`
- **logo.ico** and **logo.png** in `Assets\` (already included)

## Fastest path — one script does everything

Double-click **`BrowserCare.cmd`**. It checks the SDK, checks assets,
cleans previous output, restores, builds Release, publishes a self-contained
`win-x64` copy, and launches it — all in one run, telling you plainly if
any step fails. The built app stays at
`BrowserCareBuild\x64\BrowserCare.exe` (and `BrowserCareBuild\x86\` for 32-bit)
afterward, so you don't need
to re-run the script just to relaunch it.

## Manual commands (if you want more control)

```
dotnet restore
dotnet build
dotnet run --project BrowserCare.csproj
dotnet build -c Release
dotnet publish BrowserCare.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o BrowserCareBuild\x64
dotnet publish BrowserCare.csproj -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true -o BrowserCareBuild\x86
```

## Project layout

```
BrowserCare/
├── Assets/                       logo.ico, logo.png, wizard-large.bmp, wizard-small.bmp
├── src/
│   ├── Core/                      Models, StorageAnalyzer, SafetyEngine, ScanEngine, SettingsManager, SafetyToBrushConverter
│   ├── Chrome/                     ChromeDetector, ProfileScanner, ExtensionAnalyzer, ChromeDatabaseReader
│   ├── Cleanup/                    CacheCleaner (real Safe Cleanup delete)
│   └── Reports/                    ReportGenerator (structured text report)
├── App.xaml / App.xaml.cs          Theme, crash logging, startup temp-file cleanup
├── MainWindow.xaml / .xaml.cs      All pages: Dashboard, Profiles, Storage, History,
│                                   Downloads, Extensions, Cookies, Cleanup, Reports,
│                                   Help, Settings, Privacy, About
├── BrowserCare.csproj              Targets both win-x64 and win-x86
├── BrowserCare.sln
├── global.json                    Pins the SDK version so a parent global.json can't hijack it
├── LICENSE.txt                    Shown on the installer's License Agreement page
├── BrowserCare.cmd                 The one-file build-and-run script — builds BOTH
│                                   architectures and launches the one matching your machine
├── installer/BrowserCare.iss       Inno Setup script (compile manually, after testing the EXEs)
└── BrowserCareBuild/               Created by BrowserCare.cmd — x64/, x86/, plus a copy of
    (generated)                     BrowserCare.cmd and BrowserCare.iss for a self-contained handoff
```

## External packages

| Package | Version | Purpose |
|---|---|---|
| `Microsoft.Data.Sqlite` | 8.0.10 | Reads Chrome's History/Downloads/Cookies SQLite databases. Always opened read-only against a temp copy for browsing; direct writes (delete operations) require Chrome to be closed first and are always parameterized queries. |

No other external packages — everything else is base .NET 8 / WPF.

## Feature summary

- **Dashboard** — Quick/Deep scan with cancellable progress animation, a
  segmented Safe/Review/Protected storage bar, PC name and Chrome data path,
  and a Quick Actions rail (Cleanup/History/Reports shortcuts).
- **Profiles / Storage** — full per-profile, per-category breakdown with
  expandable detail and per-item/per-category Clean buttons.
- **History** — real SQLite reads (temp copy), keyword + date-range filter,
  Most Visited/Most Recent sort, single-account or all-accounts scope,
  delete-matching or delete-all (direct write, Chrome-must-be-closed gated).
- **Downloads** — real download records with Open/Delete per row, reverse
  search across every account, separate real Downloads-folder file scan.
- **Extensions** — real manifest-based listing grouped by account, reverse
  search ("which accounts have this extension"), explicit per-item Remove.
- **Cookies** — real per-domain counts (separate from Safe Cleanup — cookies
  stay Protected there), reverse search, explicit per-domain delete.
- **Cleanup** — detailed category-by-category preview of what Safe Cleanup
  will remove, then a real delete with a log of what was freed.
- **Reports** — Generate button renders a structured report inline; Download
  saves the same content as a `.txt` file.
- **Settings** — persisted to `%LOCALAPPDATA%\BrowserCare\settings.json`.
- **Help / Privacy / About** — static reference content; About is a fixed
  two-column layout (identity/contact + Purpose & Legal Notice).

Everything above is strictly on-demand — nothing scans or reads Chrome data
on a timer or in the background; every read happens once, on a button click.

## Security notes

- Every SQL query is parameterized (`@filter`, `@id`, `@domain`, etc.) —
  no string-concatenated SQL anywhere, even in the destructive delete paths.
- Destructive database writes (delete history/downloads/cookies) operate
  directly on the live file and require Chrome to be closed first — checked
  and blocked with a message, never silently attempted or force-killing Chrome.
- Opening a downloaded file with a known executable extension
  (`.exe`, `.bat`, `.cmd`, `.msi`, `.scr`, `.ps1`, `.vbs`, `.js`, `.jar`, `.com`)
  requires an explicit confirmation, since opening it runs it.
- Temp copies of Chrome databases (used for read-only browsing) are deleted
  immediately after use, and any left behind by a prior crashed run are
  cleaned up automatically on the next startup.
- The crash log (`%LOCALAPPDATA%\BrowserCare\crash.log`) is capped at ~1MB
  so a repeating error can't grow it without bound.
- No telemetry, no network calls — BrowserCare only ever touches local files.
- The installer requests admin once, only to place files at
  `C:\jssofttools\ChromeCare` (deliberately not under Program Files, which is
  write-protected for standard users). BrowserCare itself never needs admin
  at runtime — it only ever writes to the current user's own `%LOCALAPPDATA%`
  and Documents, never to its own install folder. Use Browse on the
  destination page during install to pick a per-user folder instead if
  you'd rather skip the admin prompt entirely.
- Builds for both 32-bit and 64-bit Windows; the installer detects the
  machine's architecture and installs the matching one automatically.

## Note on this environment

This project was built and code-reviewed line-by-line in a Linux sandbox
without a Windows machine or the .NET SDK available, so it has **not been
compiled here**. Run `BrowserCare.cmd` on your Windows machine and report
any build errors — they'll be fixed directly.
