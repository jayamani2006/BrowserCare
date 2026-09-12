<div align="center">

<!-- MAIN JS BRAND LOGO AT TOP -->
<img src="Assets/JSSTP.png" alt="JS SoftTools Products" width="720"/>

<br/><br/>

# BrowserCare™
### *Understand. Clean. Protect.*

**A High-Performance, Local-First Google Chrome™ Storage Analyzer & Safe Maintenance Suite for Windows**

<br/>

<!-- AUTO SHORTCUT DOWNLOAD BUTTON AT TOP -->
<p align="center">
  <a href="https://github.com/jayamani2006/BrowserCare/raw/main/installer-output/BrowserCare-Setup-1.0.0.exe" download="BrowserCare-Setup-1.0.0.exe">
    <img src="https://img.shields.io/badge/⚡_DOWNLOAD_BROWSERCARE_v1.0.0_(INSTALLER_EXE)-0078D6?style=for-the-badge&logo=windows&logoColor=white&labelColor=0A0A0B" alt="Download BrowserCare Installer" height="52"/>
  </a>
</p>

<p align="center">
  <b>Direct Link:</b> <a href="installer-output/BrowserCare-Setup-1.0.0.exe"><b>Download BrowserCare-Setup-1.0.0.exe (131 MB)</b></a>
  <br/>
  <i>Self-contained unified installer for Windows 10 & 11 (64-bit & 32-bit). No .NET installation required.</i>
</p>

<br/>

<!-- BADGES MATRIX -->
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%20Windows%2011-0078D6?style=flat-square&logo=windows)](https://github.com/jayamani2006/BrowserCare)
[![Architecture](https://img.shields.io/badge/Architecture-x64%20%7C%20x86-0A0A0B?style=flat-square)](https://github.com/jayamani2006/BrowserCare)
[![Framework](https://img.shields.io/badge/Framework-.NET%208%20WPF-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![Language](https://img.shields.io/badge/Language-C%23%2012-239120?style=flat-square&logo=csharp)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![Database](https://img.shields.io/badge/Database-SQLite%20(Lockless%20Snapshot)-003B57?style=flat-square&logo=sqlite)](https://www.sqlite.org/)
[![Version](https://img.shields.io/badge/Version-1.0.0%20Production-green?style=flat-square)](https://github.com/jayamani2006/BrowserCare/releases)
[![Telemetry](https://img.shields.io/badge/Telemetry-Zero%20%2F%20100%25%20Offline-brightgreen?style=flat-square)](#privacy--security-guarantee)
[![License](https://img.shields.io/badge/License-Proprietary%20EULA-red?style=flat-square)](EULA.md)

<br/>

<!-- TEAM & DEVELOPER CREDITS -->
<p align="center">
  <img src="Assets/chipX.png" alt="Chip-X Team Logo" width="240"/>
  <br/>
  <b>Engineered & Developed by:</b> <a href="https://github.com/jayamani2006"><b>Jayasubramani S</b></a> (Sole Developer & Architect)
  <br/>
  <b>Brand & Studio:</b> <b>Chip-X</b> — a <b>JS SoftTools</b> product
  <br/>
  <b>Inquiries:</b> <a href="mailto:jssofttoolproducts@gmail.com">jssofttoolproducts@gmail.com</a> • <b>Support:</b> <a href="mailto:support.jssofttoolproducts@gmail.com">support.jssofttoolproducts@gmail.com</a>
</p>

---

</div>

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [The 3-Tier Safety Engine](#the-3-tier-safety-engine)
3. [Key Architectural Pillars](#key-architectural-pillars)
4. [Complete Feature Suite (13 Dedicated Modules)](#complete-feature-suite-13-dedicated-modules)
5. [User Interface Design System](#user-interface-design-system)
6. [System Requirements](#system-requirements)
7. [Installation & Deployment](#installation--deployment)
8. [Building from Source](#building-from-source)
9. [Privacy & Security Guarantee](#privacy--security-guarantee)
10. [Developer & Studio Information](#developer--studio-information)
11. [Frequently Asked Questions (FAQ)](#frequently-asked-questions-faq)
12. [Legal Disclaimer & Trademark Notice](#legal-disclaimer--trademark-notice)

---

## Executive Summary

Google Chrome is the world's most popular web browser, yet its internal storage mechanisms are completely opaque to everyday users. Over months of routine web browsing, Chrome silently accumulates gigabytes of cached rendering shaders, compiled JavaScript/WASM bytecode, temporary media playback streams, service worker storage, and stale website databases across multiple user profiles.

Traditional system utility tools often take a blunt-force approach: they wipe entire browser directories indiscriminately. This frequently results in corrupted SQLite databases, loss of active login sessions, reset website configurations, or accidental deletion of saved browser history and bookmarks.

**BrowserCare™** introduces an intelligent, local-first inspection and maintenance architecture. It inspects Google Chrome's storage structure dynamically, categorizes every file and directory across an immutable **3-Tier Safety Hierarchy**, and provides users with complete transparency and control over what is purged.

---

## The 3-Tier Safety Engine

BrowserCare never allows arbitrary deletion. Every file, directory, and SQLite record discovered during a scan is evaluated by `SafetyEngine.cs` before any deletion action can be scheduled:

```
                            STORAGE CLASSIFICATION HIERARCHY
  ┌─────────────────────────┬─────────────────────────┬─────────────────────────┐
  │         🟢 SAFE         │        🟡 REVIEW        │       🔴 PROTECTED      │
  ├─────────────────────────┼─────────────────────────┼─────────────────────────┤
  │ Disposable Caches       │ Persistent Web Storage  │ Critical User Data      │
  │ Automatically Rebuilt   │ Affects Offline/Tokens  │ Strictly Preserved      │
  ├─────────────────────────┼─────────────────────────┼─────────────────────────┤
  │ • HTTP Web Cache        │ • IndexedDB Stores      │ • Saved Passwords       │
  │ • Compiled Code Cache   │ • Local Storage Data    │ • Active Session Cookies│
  │ • GPU / Shader Cache    │ • Service Worker Cache  │ • Bookmarks             │
  │ • Media Streaming Cache │ • Web FileSystem Data   │ • Live History Database │
  │ • Component Updates     │ • Site Favicons         │ • Downloaded User Files │
  │ • Prediction Models     │ • Quota-Managed Storage │ • Preferences & Sync    │
  ├─────────────────────────┼─────────────────────────┼─────────────────────────┤
  │ Action: Safe Cleanup    │ Action: Explicit Review │ Action: Never Deleted   │
  └─────────────────────────┴─────────────────────────┴─────────────────────────┘
```

* **🟢 Safe (Green):** Disposable temporary caches that Chrome automatically regenerates on demand. Safe to purge at any time to recover disk space with zero impact on browsing accounts.
* **🟡 Review (Yellow):** Persistent application storage used by modern web apps (IndexedDB, LocalStorage, FileSystem API). Purging these may sign you out of certain websites or reset offline drafts. Requires intentional user review.
* **🔴 Protected (Red):** Essential personal data including saved passwords (`Login Data`), active session cookies (`Cookies`), bookmarks, and live database files. **BrowserCare's Safe Cleanup engine is physically hard-locked against deleting these items.**

---

## Key Architectural Pillars

### 1. Lockless SQLite Temporary Snapshots
When Chrome is active, its SQLite database files (`History`, `Cookies`, `Web Data`) are locked by the browser process. Directly opening or writing to these files risks locking collisions and database corruption. BrowserCare solves this by creating an isolated, read-only temporary copy in `%TEMP%\browsercare_*.sqlite` before querying. These temporary files are securely deleted immediately after reading and purged on application startup.

### 2. Multi-Channel Chrome Detection
BrowserCare does not rely on hardcoded usernames or fixed drive letters. The `ChromeDetector` engine dynamically resolves user paths via standard Windows environment variables (`%LOCALAPPDATA%\Google\Chrome\User Data`) and automatically detects **Chrome Stable, Beta, Dev, and Canary** channels.

### 3. Real Multi-Profile Manifest Parsing
Rather than guessing arbitrary numbers (`Profile 1`, `Profile 2`), the `ProfileScanner` reads Chrome's underlying `Preferences` and `Local State` JSON manifests to identify authentic account display names and configured Google account emails.

### 4. Zero External Runtime Dependencies
Built with .NET 8 using WPF, BrowserCare compiles into self-contained single-file executables. It bundles its own lightweight runtime, requiring **no prior .NET runtime or SDK installation** on target client machines.

---

## Complete Feature Suite (13 Dedicated Modules)

BrowserCare features 13 purpose-built interface modules accessible from the sidebar navigation:

| View | Module | Key Capabilities & Architecture |
| :---: | :--- | :--- |
| **01** | **Dashboard** | Displays total Chrome storage consumption, a segmented Safe/Review/Protected visual breakdown bar, host PC identification, Chrome path resolution, Quick Scan (cache-only) and Deep Scan (full storage) controls, and Quick Action shortcuts. |
| **02** | **Profiles** | Detailed breakdown of every detected Chrome profile. Shows authentic user names, account emails, total disk footprint, and itemized subfolder inspection with individual clean triggers. |
| **03** | **Storage** | Global category view aggregating cache types across all profiles. Features expandable detail drawers to inspect per-profile storage distribution and category-wide cleanup triggers. |
| **04** | **History** | Direct SQLite snapshot reader for browsing history. Supports keyword and domain filtering, date range sorting (Today, 7 Days, 30 Days, All Time), visit count ranking, and safe history clearing (requires Chrome to be closed). |
| **05** | **Downloads** | Chrome download history inspector with cross-account search. Accurately distinguishes Chrome's download history records from physical files in the Windows Downloads folder. Includes an executable file launch guard (`.exe`, `.bat`, `.msi`, `.ps1`). |
| **06** | **Extensions** | Manifest v2/v3 extension auditor. Resolves extension names, localized language strings, version numbers, and actual folder size on disk per profile, with an explicit confirmation-gated removal trigger. |
| **07** | **Cookies** | Isolated per-domain cookie auditor. Displays cookie count badges by domain across all profiles. Cookies remain strictly protected from Safe Cleanup, allowing users to clear specific site cookies without blanket wiping. |
| **08** | **Cleanup** | Dedicated execution terminal. Previews exact recoverable space for Safe items, requires explicit confirmation, and streams a live monospace log of each deleted folder and recovered megabytes. |
| **09** | **Reports** | Automated audit report generator. Renders structured text diagnostics inline and exports formatted `.txt` reports with timestamped file generation (`BrowserCare-Report-YYYYMMDD-HHMMSS.txt`). |
| **10** | **Help** | Comprehensive built-in user guide explaining cache concepts, safety classifications, troubleshooting steps for locked files, and support channels. |
| **11** | **Settings** | Configuration manager storing preferences to `%LOCALAPPDATA%\BrowserCare\settings.json`. Controls Windows startup behavior, confirmation modal requirements, and inactive profile discovery. |
| **12** | **Privacy** | Formal privacy and local-first execution disclosure confirming zero telemetry and offline integrity. |
| **13** | **About** | Studio branding (Chip-X / JS SoftTools), developer attribution (Jayasubramani S), product version details, direct email support links, and legal notice. |

---

## User Interface Design System

The application interface is styled with a bespoke, dark-mode design system (`App.xaml`) engineered for clarity, speed, and contrast:

* **Background Palette:** High-contrast dark tones (`#0A0A0B` Ink, `#131315` Panel, `#1B1B1E` Card).
* **Geometric Precision:** Razor-sharp square edges across buttons and container cards, avoiding blurry non-native corner rounding.
* **Status Signaling:** Functional status colors for immediate visual clarity:
  * 🟢 **Safe:** `#4CB56E` (Safe Green)
  * 🟡 **Review:** `#E0B84C` (Review Yellow)
  * 🔴 **Protected:** `#E0665E` (Protected Red)
* **Smooth Scrolling:** Custom physics-tuned preview mouse wheel scrolling handler for responsive navigation across deep profile lists.

---

## System Requirements

| Specification | Minimum Requirement | Recommended |
| :--- | :--- | :--- |
| **Operating System** | Windows 10 (Version 1809, Build 17763 or later) | Windows 11 (22H2 or higher) |
| **Architecture** | 32-bit (x86) or 64-bit (x64) compatible processor | 64-bit (x64) processor |
| **Target Browser** | Google Chrome (Stable, Beta, Dev, Canary) | Google Chrome (Latest Release) |
| **Memory (RAM)** | 512 MB available RAM | 1 GB or higher |
| **Disk Space** | 150 MB free disk space | 300 MB free disk space |
| **Runtime Prerequisites** | **None** (Self-contained single-file binary) | **None** |

---

## Installation & Deployment

### Download the Unified Installer
Click the download button at the top of this repository or download directly:
* **[BrowserCare-Setup-1.0.0.exe](installer-output/BrowserCare-Setup-1.0.0.exe)** (131 MB)

### Installation Workflow
1. Run `BrowserCare-Setup-1.0.0.exe`.
2. Windows User Account Control (UAC) will request elevation once during setup to create the destination folder.
3. **Recommended Directory:**
   * Default: `C:\jssofttools\BrowserCare`
   * Secondary drives: `D:\BrowserCare` or `E:\BrowserCare`
   * *Note: Installing outside the write-protected `C:\Program Files` directory ensures smooth background updates and portable user configuration.*
4. The installer automatically detects whether your operating system is 32-bit or 64-bit and extracts the optimized native binary.
5. Launch BrowserCare from the desktop shortcut or Start Menu.

---

## Building from Source

### Prerequisites
* Windows 10/11 x64
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (Version 8.0.424 or higher recommended)
* Inno Setup 6 (optional, for compiling the installer package)

### 1-Click Automated Build Script
Double-click `BrowserCare.cmd` in the repository root. The automated script executes:
1. Validates .NET 8 SDK availability and version pinning (`global.json`).
2. Verifies application assets (`logo.ico`, `logo.png`).
3. Cleans previous `bin/`, `obj/`, and `BrowserCareBuild/` directories.
4. Restores NuGet packages (`Microsoft.Data.Sqlite`).
5. Compiles Release solution.
6. Publishes self-contained single-file executables for both `win-x64` and `win-x86`.
7. Prepares the `BrowserCareBuild/` distribution bundle and launches the matching build.

### Manual CLI Build Commands
```bash
# Restore dependencies
dotnet restore BrowserCare.sln

# Build Release binary
dotnet build BrowserCare.sln -c Release

# Publish 64-bit self-contained single-file executable
dotnet publish BrowserCare.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "BrowserCareBuild\x64"

# Publish 32-bit self-contained single-file executable
dotnet publish BrowserCare.csproj -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "BrowserCareBuild\x86"
```

---

## Privacy & Security Guarantee

* **100% Offline Execution:** BrowserCare never makes outbound internet connections. Zero telemetry, analytics, or tracking pings are incorporated.
* **No Credential Harvesting:** Passwords (`Login Data`), authentication cookies, autofill cards, and private browsing history are never uploaded, decrypted, or compromised.
* **Defensive Fault-Tolerance:** Any unexpected runtime exceptions are trapped by `App.xaml.cs` and written to `%LOCALAPPDATA%\BrowserCare\crash.log` (capped at 1 MB) to prevent silent application crashes.
* **Security Disclosures:** Review our comprehensive [SECURITY.md](SECURITY.md) policy for detailed vulnerability reporting guidelines.

---

## Developer & Studio Information

BrowserCare is an original software product engineered, maintained, and published under the **Chip-X** development division of **JS SoftTools**.

* **Sole Developer & Architect:** **Jayasubramani S**
  * GitHub: [@jayamani2006](https://github.com/jayamani2006)
  * Email: [jssofttoolproducts@gmail.com](mailto:jssofttoolproducts@gmail.com)
* **Product Division:** **Chip-X**
* **Parent Organization:** **JS SoftTools**
* **Technical Support:** [support.jssofttoolproducts@gmail.com](mailto:support.jssofttoolproducts@gmail.com)

---

## Frequently Asked Questions (FAQ)

#### Q: Does BrowserCare close Google Chrome automatically?
**A:** No. BrowserCare never forcefully terminates Chrome processes. If Chrome is currently running, BrowserCare will gracefully skip locked cache files and prompt you if an action (such as clearing history or deleting cookies) requires Chrome to be closed.

#### Q: Will Safe Cleanup log me out of my accounts?
**A:** No. Session tokens and sign-in cookies are strictly categorized as **🔴 Protected**. Safe Cleanup purges only disposable render caches, shader binaries, and temporary media streams.

#### Q: Why is the installer ~130 MB?
**A:** BrowserCare is packaged as a **self-contained deployment**. It embeds the complete native .NET 8 desktop runtime for both 64-bit and 32-bit systems, ensuring that end users do not need to download or configure any external .NET dependencies.

#### Q: Can BrowserCare clean other Chromium browsers (Brave, Edge, Opera)?
**A:** Version 1.0.0 is specifically engineered and tested for Google Chrome. Support for Microsoft Edge and Brave is planned for upcoming releases.

---

## Legal Disclaimer & Trademark Notice

*BrowserCare is an independent software utility engineered by Jayasubramani S (Chip-X / JS SoftTools). BrowserCare is not affiliated with, authorized by, sponsored by, or endorsed by Google LLC or Alphabet Inc. Google Chrome™ is a registered trademark of Google LLC.*

*Copyright © 2026 Jayasubramani S — Chip-X / JS SoftTools. All rights reserved. Distributed under the terms of the [BrowserCare End User License Agreement (EULA)](EULA.md).*
