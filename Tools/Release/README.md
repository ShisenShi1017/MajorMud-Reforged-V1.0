# RealmGate Release Packaging

## Cross-platform tester preview

`Build-RealmGateDesktopPackage.ps1` creates the public Avalonia desktop preview for either
Windows or Linux. The archives are self-contained, Borin-free, and contain the compact
authoritative Librarium rather than generated `Data/`. Before packaging, the script proves that
the staged Librarium can regenerate every enabled world without being modified.

```powershell
./Tools/Release/Build-RealmGateDesktopPackage.ps1 -Version 0.2.0-beta.1 -RuntimeIdentifier win-x64
./Tools/Release/Build-RealmGateDesktopPackage.ps1 -Version 0.2.0-beta.1 -RuntimeIdentifier linux-x64
```

Native packages should be built on their matching operating system so executable permissions and
platform assets are retained. Output is written to `Release/DesktopPackages/` as a Windows ZIP or
Linux `tar.gz`, with a SHA-256 checksum beside it.

The `RealmGate Desktop Tester Release` GitHub workflow builds both packages on native runners. A
manual run can either leave them as 30-day workflow artifacts or create a GitHub prerelease with
both packages attached. The Linux tester does not need to install .NET; instructions are included
inside the archive.

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

That workflow remains the transitional WPF/Windows release. New Windows/Linux tester previews use
`RealmGate Desktop Tester Release`.
