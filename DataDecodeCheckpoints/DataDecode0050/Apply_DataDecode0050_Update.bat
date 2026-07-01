@echo off
echo Applying DataDecode0050 documentation delta...
set TARGET=%~1
if "%TARGET%"=="" set TARGET=.
if not exist "%TARGET%" mkdir "%TARGET%"
copy /Y "AI_CONTEXT_DataDecode0050_Append.md" "%TARGET%\AI_CONTEXT_DataDecode0050_Append.md" >nul
copy /Y "CHANGELOG_DataDecode0050_Append.md" "%TARGET%\CHANGELOG_DataDecode0050_Append.md" >nul
copy /Y "VERSION_DataDecode0050_Append.md" "%TARGET%\VERSION_DataDecode0050_Append.md" >nul
copy /Y "ReleaseNotes_DataDecode0050_Append.md" "%TARGET%\ReleaseNotes_DataDecode0050_Append.md" >nul
copy /Y "Roadmap_DataDecode0050_Append.md" "%TARGET%\Roadmap_DataDecode0050_Append.md" >nul
echo Done.
