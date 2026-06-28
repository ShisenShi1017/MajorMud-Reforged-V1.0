@echo off
setlocal EnableExtensions
set "SCRIPT_DIR=%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%Apply-DatabaseVersion-0002.ps1"
if errorlevel 1 (
  echo.
  echo Database Version 0002 failed. See the error above.
  pause
  exit /b 1
)
echo.
echo Database Version 0002 applied successfully.
pause
