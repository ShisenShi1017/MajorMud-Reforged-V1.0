@echo off
cd /d "%~dp0\.."
powershell -NoProfile -ExecutionPolicy Bypass -File "HostEditor-PC\Editor\MajorMUDEditor-GUI.ps1"
