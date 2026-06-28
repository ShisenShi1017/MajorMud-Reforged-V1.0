@echo off
setlocal

REM Installs the Reforged GitHub Actions workflow into this repository.
REM Run this file from the repository root on Windows.

cd /d "%~dp0"

if not exist ".github" mkdir ".github"
if not exist ".github\workflows" mkdir ".github\workflows"

if not exist "GitHubActions\dotnet-build.yml" (
    echo ERROR: GitHubActions\dotnet-build.yml was not found.
    echo Make sure you extracted the full CodeUpdate ZIP before running this file.
    pause
    exit /b 1
)

copy /Y "GitHubActions\dotnet-build.yml" ".github\workflows\dotnet-build.yml" >nul
if errorlevel 1 (
    echo ERROR: Could not copy workflow file.
    pause
    exit /b 1
)

echo Installed .github\workflows\dotnet-build.yml

echo.
echo Next steps:
echo   1. Commit this repository to GitHub.
echo   2. Push to main or master.
echo   3. Open the Actions tab and run/check "Reforged Cloud Build".
echo.
pause
exit /b 0
