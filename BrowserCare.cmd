@echo off
setlocal EnableDelayedExpansion
title BrowserCare
cd /d "%~dp0"

echo ========================================
echo BROWSERCARE - BUILD AND RUN
echo ========================================
echo Builds a self-contained, single-file BrowserCare.exe for BOTH 64-bit
echo and 32-bit Windows (covers Windows 10 32/64-bit and Windows 11
echo 64-bit — Windows 11 was never released as 32-bit), then launches the
echo version matching this machine.
echo.

echo [1/8] Checking .NET SDK...
where dotnet >nul 2>nul
set "EC=%ERRORLEVEL%"
if not "%EC%"=="0" (
    echo   X dotnet was not found on PATH.
    echo.
    echo   Install the .NET 8 SDK from:
    echo   https://dotnet.microsoft.com/download/dotnet/8.0
    goto :fail
)
dotnet --version > "%TEMP%\bc_dotnet_version.txt" 2>&1
set "EC=%ERRORLEVEL%"
set /p DOTNET_VERSION=<"%TEMP%\bc_dotnet_version.txt"
if not "%EC%"=="0" (
    echo   X .NET SDK resolution failed:
    type "%TEMP%\bc_dotnet_version.txt"
    echo.
    echo   This usually means a global.json ABOVE this folder pins an SDK
    echo   version you don't have. This project ships its own global.json
    echo   to fix that - make sure it wasn't deleted.
    goto :fail
)
echo   OK .NET SDK found: %DOTNET_VERSION%
echo.

echo [2/8] Checking project files...
if not exist "BrowserCare.csproj" (
    echo   X BrowserCare.csproj not found. Run this script from the project root.
    goto :fail
)
echo   OK Project found
echo.

echo [3/8] Checking assets...
if exist "Assets\logo.ico" (
    powershell -NoProfile -Command "$b=[System.IO.File]::ReadAllBytes('Assets\logo.ico')[0..3]; if($b[0] -eq 0 -and $b[1] -eq 0 -and $b[2] -eq 1 -and $b[3] -eq 0){exit 0}else{exit 1}"
    if not "!ERRORLEVEL!"=="0" (
        echo   X Assets\logo.ico exists but is not a real ICO file.
        goto :fail
    )
    echo   OK logo.ico
) else (
    echo   ! logo.ico missing - the build will still work, but without an app icon.
)
if exist "Assets\logo.png" (
    echo   OK logo.png
) else (
    echo   ! logo.png missing - splash/About branding will be blank.
)
echo.

echo [4/8] Cleaning previous build output...
if exist "bin" rmdir /s /q "bin"
if exist "obj" rmdir /s /q "obj"
if exist "BrowserCareBuild" rmdir /s /q "BrowserCareBuild"
echo   OK Cleaned
echo.

echo [5/8] Restoring NuGet packages...
dotnet restore BrowserCare.sln
set "EC=%ERRORLEVEL%"
if not "%EC%"=="0" (
    echo   X Restore failed with exit code %EC%.
    goto :fail
)
echo   OK Restored
echo.

echo [6/8] Building Release configuration...
dotnet build BrowserCare.sln -c Release
set "EC=%ERRORLEVEL%"
if not "%EC%"=="0" (
    echo   X Build failed with exit code %EC%.
    goto :fail
)
echo   OK Built
echo.

echo [7/8] Publishing self-contained single-file executables (x64 and x86)...
echo   - 64-bit build (for 64-bit Windows 10 and all Windows 11)...
dotnet publish BrowserCare.csproj -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true ^
    -o "BrowserCareBuild\x64"
set "EC=%ERRORLEVEL%"
if not "%EC%"=="0" (
    echo   X x64 publish failed with exit code %EC%.
    goto :fail
)
if not exist "BrowserCareBuild\x64\BrowserCare.exe" (
    echo   X BrowserCareBuild\x64\BrowserCare.exe was not produced.
    goto :fail
)
for %%F in ("BrowserCareBuild\x64\BrowserCare.exe") do set "SIZE_X64=%%~zF"
if !SIZE_X64! LSS 40000000 (
    echo   X BrowserCareBuild\x64\BrowserCare.exe is only !SIZE_X64! bytes (under 40MB).
    echo     The .NET runtime was NOT bundled properly (binary is framework-dependent).
    goto :fail
)
echo     OK x64 build ready (!SIZE_X64! bytes - self-contained)

echo   - 32-bit build (for 32-bit Windows 10)...
dotnet publish BrowserCare.csproj -c Release -r win-x86 --self-contained true ^
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true ^
    -o "BrowserCareBuild\x86"
set "EC=%ERRORLEVEL%"
if not "%EC%"=="0" (
    echo   X x86 publish failed with exit code %EC%.
    goto :fail
)
if not exist "BrowserCareBuild\x86\BrowserCare.exe" (
    echo   X BrowserCareBuild\x86\BrowserCare.exe was not produced.
    goto :fail
)
for %%F in ("BrowserCareBuild\x86\BrowserCare.exe") do set "SIZE_X86=%%~zF"
if !SIZE_X86! LSS 40000000 (
    echo   X BrowserCareBuild\x86\BrowserCare.exe is only !SIZE_X86! bytes (under 40MB).
    echo     The .NET runtime was NOT bundled properly (binary is framework-dependent).
    goto :fail
)
echo     OK x86 build ready (!SIZE_X86! bytes - self-contained)
echo.

echo [8/8] Preparing handoff folder...
REM Copy this script and the installer files directly into BrowserCareBuild\
REM (not nested in an installer\ subfolder), so BrowserCareBuild\ is a single,
REM complete, self-contained folder — everything needed to test the app AND
REM build the installer is right here, with paths that work from THIS location.
copy /y "BrowserCare.cmd" "BrowserCareBuild\BrowserCare.cmd" >nul
copy /y "installer\BrowserCare.iss" "BrowserCareBuild\BrowserCare.iss" >nul
if exist "LICENSE.txt" copy /y "LICENSE.txt" "BrowserCareBuild\LICENSE.txt" >nul
if exist "Assets\wizard-large.bmp" (
    if not exist "BrowserCareBuild\Assets" mkdir "BrowserCareBuild\Assets"
    copy /y "Assets\wizard-large.bmp" "BrowserCareBuild\Assets\" >nul
    copy /y "Assets\wizard-small.bmp" "BrowserCareBuild\Assets\" >nul
    copy /y "Assets\logo.ico" "BrowserCareBuild\Assets\" >nul
)
REM The copied .iss references its build folders as "..\BrowserCareBuild\x64\"
REM etc., correct only from installer\BrowserCare.iss at the project root. The
REM copy sitting in BrowserCareBuild\ itself needs those paths rewritten to be
REM relative to ITS OWN location instead (x64\ is a direct sibling there, not
REM two levels up) — otherwise compiling the handoff copy would silently look
REM in the wrong place.
powershell -NoProfile -Command ^
    "(Get-Content 'BrowserCareBuild\BrowserCare.iss') -replace '\.\.\\BrowserCareBuild\\', '' -replace '\.\.\\Assets\\', 'Assets\' -replace '\.\.\\LICENSE\.txt', 'LICENSE.txt' | Set-Content 'BrowserCareBuild\BrowserCare.iss'"
echo   OK Handoff folder ready: BrowserCareBuild\
echo.

echo ========================================
echo BUILD COMPLETE
echo ========================================
echo   BrowserCareBuild\x64\BrowserCare.exe   (64-bit Windows 10/11)
echo   BrowserCareBuild\x86\BrowserCare.exe   (32-bit Windows 10)
echo.
echo Launching the version that matches this machine...

REM Detect whether THIS machine is 64-bit or 32-bit and launch the matching build.
if defined PROCESSOR_ARCHITEW6432 (
    set "ARCH=x64"
) else if "%PROCESSOR_ARCHITECTURE%"=="AMD64" (
    set "ARCH=x64"
) else (
    set "ARCH=x86"
)
start "" "BrowserCareBuild\%ARCH%\BrowserCare.exe"
echo Launched the %ARCH% build. This window can be closed.
echo.
echo To build the installer: open BrowserCareBuild\installer\BrowserCare.iss
echo in Inno Setup and compile it. It automatically picks the right
echo architecture's build for whoever runs the installer.
echo.
pause
exit /b 0

:fail
echo.
echo ========================================
echo BrowserCare build stopped due to an error.
echo ========================================
pause
exit /b 1
