@echo off
cd /d "%~dp0"
title MajorMUD Local Client v1.1
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Client.ps1"
pause
