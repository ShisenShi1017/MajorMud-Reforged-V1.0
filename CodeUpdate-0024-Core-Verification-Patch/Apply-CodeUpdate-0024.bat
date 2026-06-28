@echo off
setlocal EnableExtensions
echo Applying CodeUpdate-0024 - Core Verification
set "UPDATE_DIR=%~dp0"
for %%I in ("%UPDATE_DIR%..") do set "REPO_ROOT=%%~fI"
echo Update folder: %UPDATE_DIR%
echo Target repository: %REPO_ROOT%
if not exist "%REPO_ROOT%\README.md" (
  echo.
  echo WARNING: README.md was not found in the target folder.
  echo Extract this ZIP directly inside the main repository folder, then run this BAT from the extracted update folder.
  echo.
  pause
)
call :copyfile "UpdateManifest.json"
call :copyfile ".github\workflows\dotnet-build.yml"
call :copyfile "AI_CONTEXT.md"
call :copyfile "CHANGELOG.md"
call :copyfile "Core\Source\Reforged.Core\Diagnostics\CoreStartupDiagnostics.cs"
call :copyfile "Core\Source\Reforged.Core\Diagnostics\StartupDiagnosticReport.cs"
call :copyfile "Core\Source\Reforged.Core\Events\EventPublishedEvent.cs"
call :copyfile "Core\Source\Reforged.Core\Events\IEventPublisher.cs"
call :copyfile "Core\Source\Reforged.Core\Events\IReforgedEvent.cs"
call :copyfile "Core\Source\Reforged.Core\Events\InMemoryEventBus.cs"
call :copyfile "Core\Source\Reforged.Core\Events\ReforgedEventBase.cs"
call :copyfile "Docs\CodeUpdates\CodeUpdate-0024-Core-Verification.md"
call :copyfile "GitHubActions\dotnet-build.yml"
call :copyfile "ProjectManifest.json"
call :copyfile "README.md"
call :copyfile "Tests\Source\Reforged.Core.Tests\Program.cs"
call :copyfile "Tests\Source\Reforged.Core.Tests\Reforged.Core.Tests.csproj"
call :copyfile "VERSION.md"
echo.
echo CodeUpdate-0024 applied. Push to GitHub and verify with GitHub Actions.
echo.
pause
exit /b 0

:copyfile
set "REL=%~1"
for %%F in ("%REPO_ROOT%\%REL%") do if not exist "%%~dpF" mkdir "%%~dpF"
copy /Y "%UPDATE_DIR%%REL%" "%REPO_ROOT%\%REL%" >nul
if errorlevel 1 (
  echo FAILED: %REL%
  pause
  exit /b 1
) else (
  echo Copied: %REL%
)
exit /b 0
