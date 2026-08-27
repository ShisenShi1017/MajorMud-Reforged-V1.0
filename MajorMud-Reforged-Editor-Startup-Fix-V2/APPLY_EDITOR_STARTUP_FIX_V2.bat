@echo off
setlocal
cd /d "%~dp0"
title MajorMUD Reforged - Editor Startup Fix V2

echo ============================================================
echo  MajorMUD Reforged - Editor Startup Fix V2
echo ============================================================
echo.
echo Fixes the BaseRecordJson WPF startup crash, removes stale
echo WPF build output, then BUILDS AND PUBLISHES a fresh app.
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0apply_editor_startup_fix_v2.ps1"
set "RC=%ERRORLEVEL%"

echo.
if "%RC%"=="0" (
    echo ============================================================
    echo  FIX COMPLETE
    echo ============================================================
    echo.
    echo Run START_FIXED_APP.bat to launch the newly published app.
) else (
    echo Fix stopped with error code %RC%.
    echo Nothing else should be changed until the message above is checked.
)
echo.
pause
exit /b %RC%
