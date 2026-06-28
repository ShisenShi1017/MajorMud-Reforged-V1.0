@echo off
cd /d "%~dp0\Editor"
powershell -NoProfile -ExecutionPolicy Bypass -File ".\MajorMUD-Editor.ps1"
