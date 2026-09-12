; BrowserCare Inno Setup script.
;
; Build first with BrowserCare.cmd — it publishes TWO self-contained,
; single-file executables (one for 64-bit Windows, one for 32-bit Windows)
; into BrowserCareBuild\x64\ and BrowserCareBuild\x86\. This script picks
; the correct one for the machine it's running on automatically, so a
; single installer covers both Windows 10 (32-bit or 64-bit) and
; Windows 11 (64-bit only — Microsoft never released a 32-bit Windows 11).
;
; Not run automatically as part of the dev workflow — build and test the
; EXE(s) first, then compile this script manually with Inno Setup.

#define MyAppName "BrowserCare"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "JS SoftTools"
#define MyAppBrand "Chip-X"
#define MyAppExeName "BrowserCare.exe"
#define MyAppURL ""

[Setup]
AppId={{42D9FF12-F4C6-4EF1-80E4-207DB103BD91}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}

; Installs to C:\jssofttools\BrowserCare by default — deliberately NOT under
; Program Files. Program Files is write-protected for standard users, which
; is exactly the "read and write not working in protected folders" problem
; this avoids. This top-level folder is a normal, simple location.
DefaultDirName=C:\jssofttools\BrowserCare
; Explicitly enabled (this is also Inno's default) so the wizard shows the
; "Select Destination Location" page and the user can Browse to a different
; folder if they'd rather install somewhere else (e.g. per-user, no admin).
DisableDirPage=no

DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\installer-output
OutputBaseFilename=BrowserCare-Setup-{#MyAppVersion}
SetupIconFile=..\Assets\logo.ico
WizardImageFile=..\Assets\wizard-large.bmp
WizardSmallImageFile=..\Assets\wizard-small.bmp
LicenseFile=..\LICENSE.txt
Compression=lzma
SolidCompression=yes
WizardStyle=modern

; Covers both 32-bit and 64-bit Windows 10, and 64-bit Windows 11.
ArchitecturesAllowed=x86compatible or x64compatible
; Windows 10 version 1809 (October 2018 Update) or later — matches the
; .NET 8 desktop runtime's practical minimum for Windows 10.
MinVersion=10.0.17763

; Writing to C:\jssofttools\ requires elevation on a standard Windows setup
; (the root of C:\ is write-protected for normal users), so the INSTALLER
; asks for admin once, during setup only. BrowserCare itself never needs
; admin rights afterward — at runtime it only ever writes to the current
; user's own %LOCALAPPDATA% (settings, crash log) and Documents (reports),
; never to its own install folder. If you'd rather avoid the admin prompt
; entirely, use Browse on the destination page to pick a per-user folder
; instead (e.g. one under your own AppData or Documents).
PrivilegesRequired=admin

UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
; 64-bit Windows gets the x64 build.
Source: "..\BrowserCareBuild\x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: Is64BitInstallMode
; 32-bit Windows gets the x86 build. Is64BitInstallMode is false there, so
; only one of these two Source lines ever actually installs anything.
Source: "..\BrowserCareBuild\x86\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: not Is64BitInstallMode

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
// Sanity check before the wizard even starts: make sure the matching build
// output actually exists for this machine's architecture, and fail with a
// clear message instead of a confusing "no files to install" partway through.
function InitializeSetup(): Boolean;
var
  BuildFolder: String;
begin
  if Is64BitInstallMode then
    BuildFolder := ExpandConstant('{src}\..\BrowserCareBuild\x64\{#MyAppExeName}')
  else
    BuildFolder := ExpandConstant('{src}\..\BrowserCareBuild\x86\{#MyAppExeName}');

  if not FileExists(BuildFolder) then
  begin
    MsgBox('Build output not found:' + #13#10 + BuildFolder + #13#10#13#10 +
           'Run BrowserCare.cmd first to build both the x64 and x86 versions, ' +
           'then compile this installer.', mbError, MB_OK);
    Result := False;
  end
  else
    Result := True;
end;
