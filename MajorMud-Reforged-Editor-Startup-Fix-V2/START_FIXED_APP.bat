@echo off
setlocal
cd /d "%~dp0"
title MajorMUD Reforged - Start Fixed App
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0start_fixed_app.ps1"
set "RC=%ERRORLEVEL%"
if not "%RC%"=="0" pause
exit /b %RC%
