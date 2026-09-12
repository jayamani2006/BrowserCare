# BrowserCare — Release Checklist

Run through this before creating the Inno Setup installer.

## Application
- [ ] BrowserCare launches (`BrowserCare.cmd`)
- [ ] Logo appears correctly (icon + Dashboard header)
- [ ] No missing assets
- [ ] No crashes during a normal scan
- [ ] Dashboard shows real totals after Quick Scan
- [ ] Dashboard shows real totals after Deep Scan
- [ ] Chrome detection works (and shows a clear message when Chrome isn't installed)
- [ ] Profile detection works (correct names, correct sizes)
- [ ] Cancel Scan actually stops a running scan
- [ ] Profiles page shows every profile's real item breakdown
- [ ] Storage page shows correct category totals across profiles
- [ ] History analyzer returns real counts (compare against chrome://history if unsure)
- [ ] Downloads analyzer shows Chrome records and the real Downloads folder separately
- [ ] Extensions page lists real installed extensions with correct names/sizes
- [ ] Extension Remove actually removes the extension and refreshes the list
- [ ] Safe Cleanup actually deletes Safe items and reports an accurate recovered amount
- [ ] Reports page exports a real TXT file and "Open Folder" locates it
- [ ] Settings persist after closing and reopening BrowserCare
- [ ] Help, Privacy, and About pages display correctly
- [ ] Contact/Support buttons open the default mail client

## Not yet in this phase (do not check these off until implemented)
- [ ] JSON/HTML report export
- [ ] Backup/restore before Review-level cleanup
- [ ] Built-in terminal
- [ ] Duplicate/abandoned profile detection

## Safety
- [ ] Passwords protected (never appear as cleanable)
- [ ] Cookies protected
- [ ] Bookmarks protected
- [ ] Actual Downloads folder files protected (Safe Cleanup never touches them)
- [ ] Protected Chrome databases not accidentally scanned as deletable
- [ ] Chrome-running detection works before Safe Cleanup
- [ ] Locked files handled gracefully (appear in "skipped", app doesn't crash)
- [ ] Extension removal requires explicit confirmation every time

## Release
- [ ] `BrowserCare.cmd` succeeds
- [ ] `BrowserCare.exe` exists in both `BrowserCareBuild\x64\` and `BrowserCareBuild\x86\`
- [ ] The x64 build launches on a 64-bit Windows machine
- [ ] The x86 build launches on a 32-bit Windows machine (or a 64-bit one — x86 binaries run there too)
- [ ] EXE launches on a clean Windows test machine (no dev tools installed)
- [ ] Correct icon is embedded on the EXE
- [ ] No development paths hard-coded
- [ ] No personal Windows username hard-coded
- [ ] No debug-only code enabled

Only after every applicable box above is checked should the Inno Setup
installer (`installer\BrowserCare.iss`) be compiled.
