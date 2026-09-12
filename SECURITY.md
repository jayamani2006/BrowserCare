# Security Policy — BrowserCare™

## Security Overview

**BrowserCare™** is engineered from the ground up as a **100% local-first, zero-telemetry desktop utility**. The software operates strictly on your local machine without sending data across any network interface.

---

## Core Security & Privacy Commitments

1. **Zero Network Communication:**
   BrowserCare contains no analytics SDKs, no tracking pixels, and no network pingbacks. It functions completely offline.
2. **Read-Only SQLite Snapshots:**
   To inspect Chrome's History, Downloads, and Cookies without risking file locks or database corruption, BrowserCare makes an isolated, read-only temporary copy in `%TEMP%\browsercare_*.sqlite`. These files are deleted immediately after reading, and any orphaned files from crashed sessions are purged upon startup.
3. **Protected Data Isolation:**
   Sensitive credentials (saved passwords in `Login Data`, payment autofill in `Web Data`, active session tokens, and bookmarks) are classified as **🔴 Protected** by the `SafetyEngine`. The Safe Cleanup mechanism is strictly prohibited from modifying or deleting these files.
4. **Parameterized SQL Queries:**
   All SQLite queries within `ChromeDatabaseReader.cs` use parameterized SQL statements (`@filter`, `@id`, `@domain`). No SQL injection vulnerabilities exist in read or write operations.
5. **Executable Safety Confirmation:**
   When browsing downloaded items in the Downloads inspector, opening any file with an executable extension (`.exe`, `.bat`, `.cmd`, `.msi`, `.ps1`, `.vbs`, etc.) triggers an explicit modal confirmation to prevent accidental malware execution.
6. **No Administrator Rights Required at Runtime:**
   While the installer may request elevation once to create `C:\jssofttools\BrowserCare`, BrowserCare itself runs under standard user privileges. All runtime logs (`crash.log`) and configuration files (`settings.json`) are stored within `%LOCALAPPDATA%\BrowserCare`.

---

## Reporting a Security Vulnerability

If you discover a potential security flaw, vulnerability, or privacy leak within BrowserCare, please report it privately:

* **Security Email:** [support.jssofttoolproducts@gmail.com](mailto:support.jssofttoolproducts@gmail.com)
* **General Correspondence:** [jssofttoolproducts@gmail.com](mailto:jssofttoolproducts@gmail.com)
* **Lead Developer:** Jayasubramani S (Chip-X / JS SoftTools)

Please provide:
- A detailed description of the vulnerability.
- Steps to reproduce the issue.
- Operating system version and build (e.g., Windows 11 23H2).
- BrowserCare version.

**Do NOT include personal passwords, browsing history, or sensitive credentials in your bug report.** All legitimate security inquiries will receive a response within 48 business hours.
