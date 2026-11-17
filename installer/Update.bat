@echo off
setlocal enabledelayedexpansion
title LuxPresenter - Patch Updater

echo ========================================================
echo             LuxPresenter Quick Patch Updater
echo ========================================================
echo.

:: 1. Close LuxPresenter if running
echo [1/4] Closing LuxPresenter if running...
taskkill /F /IM LuxPresenter.exe >nul 2>&1
taskkill /F /IM soffice.exe >nul 2>&1
taskkill /F /IM soffice.bin >nul 2>&1
ping 127.0.0.1 -n 2 >nul 2>&1

:: 2. Locate installation directory
echo [2/4] Locating LuxPresenter installation folder...
set "TARGET_DIR="

:: Check if running directly inside the installation directory
if exist "%~dp0Qt6Core.dll" if exist "%~dp0LuxPresenter.exe" set "TARGET_DIR=%~dp0"

:: Check Inno Setup registry entries
if not defined TARGET_DIR for /f "tokens=2* delims=	 " %%A in ('reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\{D1A3961E-745B-4C6A-914F-94B5E8BC2901}_is1" /v InstallLocation 2^>nul') do if exist "%%B\LuxPresenter.exe" set "TARGET_DIR=%%B"
if not defined TARGET_DIR for /f "tokens=2* delims=	 " %%A in ('reg query "HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall\{D1A3961E-745B-4C6A-914F-94B5E8BC2901}_is1" /v InstallLocation 2^>nul') do if exist "%%B\LuxPresenter.exe" set "TARGET_DIR=%%B"

:: Check standard installation locations
if not defined TARGET_DIR if exist "%LocalAppData%\Programs\LuxPresenter\LuxPresenter.exe" set "TARGET_DIR=%LocalAppData%\Programs\LuxPresenter"
if not defined TARGET_DIR if exist "%ProgramFiles%\LuxPresenter\LuxPresenter.exe" set "TARGET_DIR=%ProgramFiles%\LuxPresenter"
if not defined TARGET_DIR if exist "C:\Program Files (x86)\LuxPresenter\LuxPresenter.exe" set "TARGET_DIR=C:\Program Files (x86)\LuxPresenter"
if not defined TARGET_DIR if exist "E:\LuxPresenter-QT6\Portable\LuxPresenter.exe" set "TARGET_DIR=E:\LuxPresenter-QT6\Portable"

:: If still not defined, ask user
if not defined TARGET_DIR (
    echo [PROMPT] Could not automatically detect LuxPresenter installation directory.
    set /p "TARGET_DIR=Please enter the path to your LuxPresenter installation folder: "
    if defined TARGET_DIR set "TARGET_DIR=!TARGET_DIR:"=!"
)

if not defined TARGET_DIR goto :NOT_FOUND
if not exist "!TARGET_DIR!\LuxPresenter.exe" goto :NOT_FOUND

:: Check admin privileges for Program Files
net session >nul 2>&1
if errorlevel 1 (
    echo !TARGET_DIR! | findstr /i /c:"Program Files" >nul
    if not errorlevel 1 (
        echo [INFO] Administrator permission required to update Program Files.
        echo Requesting elevation...
        powershell -NoProfile -Command "Start-Process cmd.exe -ArgumentList '/c \"\"%~f0\"\"' -Verb RunAs"
        exit /b
    )
)

echo Found installation at: "!TARGET_DIR!"
echo.

:: 3. Copy application binaries, libraries, and dependencies
echo [3/4] Applying application and runtime updates...
if exist "%~dp0LuxPresenter.exe" (
    copy /Y "%~dp0LuxPresenter.exe" "!TARGET_DIR!\LuxPresenter.exe" >nul
    if errorlevel 1 goto :COPY_FAILED
)

:: Copy any updated DLLs and assets
copy /Y "%~dp0*.dll" "!TARGET_DIR!\" >nul 2>&1
if exist "%~dp0app.ico" copy /Y "%~dp0app.ico" "!TARGET_DIR!\app.ico" >nul 2>&1

:: Copy updated Bible database if included in patch
if exist "%~dp0Data\bibles.json" (
    if not exist "!TARGET_DIR!\Data" mkdir "!TARGET_DIR!\Data" >nul 2>&1
    copy /Y "%~dp0Data\bibles.json" "!TARGET_DIR!\Data\bibles.json" >nul
    if exist "%AppData%\LuxPresenterQt6\Data" (
        copy /Y "%~dp0Data\bibles.json" "%AppData%\LuxPresenterQt6\Data\bibles.json" >nul
    )
    if exist "%AppData%\LuxPresenter\Data" (
        copy /Y "%~dp0Data\bibles.json" "%AppData%\LuxPresenter\Data\bibles.json" >nul
    )
)

:: Copy browser extension updates if included in patch
if exist "%~dp0Extensions\uBlockOrigin" (
    echo [4/4] Updating browser ad-blocker extensions and cleaning unused components...
    robocopy "%~dp0Extensions\uBlockOrigin" "!TARGET_DIR!\Extensions\uBlockOrigin" /E /NFL /NDL /NJH /NJS /nc /ns /np >nul 2>&1
) else (
    echo [4/4] Finalizing patch and cleaning unused components...
)

:: Clean up unused Edge/Copilot bloat in Engines\WebView2 if present
if exist "!TARGET_DIR!\Engines\WebView2" (
    del /f /q "!TARGET_DIR!\Engines\WebView2\*\Copilot*" >nul 2>&1
    del /f /q "!TARGET_DIR!\Engines\WebView2\*\*copilot*" >nul 2>&1
    del /f /q "!TARGET_DIR!\Engines\WebView2\*\notification_helper.exe" >nul 2>&1
    del /f /q "!TARGET_DIR!\Engines\WebView2\*\platform_experiences_helper.exe" >nul 2>&1
    del /f /q "!TARGET_DIR!\Engines\WebView2\*\telclient.dll" >nul 2>&1
    del /f /q "!TARGET_DIR!\Engines\WebView2\*\oneds.dll" >nul 2>&1
    del /f /q "!TARGET_DIR!\Engines\WebView2\*\oneauth.dll" >nul 2>&1
    del /f /q "!TARGET_DIR!\Engines\WebView2\*\onnxruntime.dll" >nul 2>&1
    del /f /q "!TARGET_DIR!\Engines\WebView2\*\mip_core_gn.dll" >nul 2>&1
    del /f /q "!TARGET_DIR!\Engines\WebView2\*\dual_engine_adapter_x64.dll" >nul 2>&1
    del /f /q "!TARGET_DIR!\Engines\WebView2\*\learning_tools.dll" >nul 2>&1
    del /f /q "!TARGET_DIR!\Engines\WebView2\*\Locales\*copilot*" >nul 2>&1
    rd /s /q "!TARGET_DIR!\Engines\WebView2\*\edge_feedback" >nul 2>&1
    rd /s /q "!TARGET_DIR!\Engines\WebView2\*\Installer" >nul 2>&1
)

echo.
echo ========================================================
echo   UPDATE SUCCESSFUL: LuxPresenter is now up to date.
echo ========================================================
echo.
pause
exit /b 0

:NOT_FOUND
echo.
echo ========================================================
echo [ERROR] LuxPresenter installation folder was not found.
echo Please copy the patch files manually into your
echo LuxPresenter installation folder.
echo ========================================================
echo.
pause
exit /b 1

:COPY_FAILED
echo.
echo ========================================================
echo [ERROR] Failed to replace LuxPresenter.exe.
echo Please make sure the app is closed. If installed in
echo Program Files, right-click 'Update.bat' and select
echo 'Run as administrator'.
echo ========================================================
echo.
pause
exit /b 1
