@echo off
cd /d "%~dp0"
title MajorMUD Server v1.1
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Server.ps1"
pause
