@echo off
setlocal
set SCRIPT_DIR=%~dp0
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%Apply-CodeUpdate-0041.ps1"
if errorlevel 1 (
  echo CodeUpdate-0041 failed.
  exit /b 1
)
echo CodeUpdate-0041 applied successfully.
exit /b 0
