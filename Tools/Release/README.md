# RealmGate Release Packaging

## Public beta

Build a normal Windows public beta package with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Release\Build-RealmGateInstaller.ps1 -Version 0.1.0-beta.2
```

The public beta build is Borin-free and self-contained. Testers do not need to install the .NET Desktop Runtime separately.

Output is written to `Release\Packages\`:

- `RealmGate-Beta-<version>-Setup.exe` — recommended download for normal testers.
- `RealmGate-Beta-<version>-win-x64-portable.zip` — optional portable copy for advanced users.
- `RealmGate-Beta-<version>-SHA256.txt` — checksums for both packages.

The installer installs per-user under `%LOCALAPPDATA%\Programs\RealmGate`, creates a Start Menu shortcut, offers an optional desktop shortcut, and registers an uninstaller.

## Publish only

`Publish-RealmGate.ps1 -PublicRelease` now creates a self-contained `win-x64` public build by default. Use `-FrameworkDependent` only when intentionally producing a build that requires a separately installed .NET runtime.

## GitHub Actions

The `RealmGate Public Beta Installer` workflow builds and uploads the installer, portable ZIP, and checksums as a workflow artifact. Run it manually and supply the beta version when preparing a release.
