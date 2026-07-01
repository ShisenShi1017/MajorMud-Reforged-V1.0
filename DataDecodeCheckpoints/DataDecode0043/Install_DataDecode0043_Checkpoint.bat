@echo off
setlocal
set SRC=%~dp0
set DEST=%CD%\DataDecodeCheckpoints\DataDecode0043
if not exist "%DEST%" mkdir "%DEST%"
copy /Y "%SRC%*" "%DEST%" >nul
echo DataDecode0043 checkpoint files copied to %DEST%.
endlocal
