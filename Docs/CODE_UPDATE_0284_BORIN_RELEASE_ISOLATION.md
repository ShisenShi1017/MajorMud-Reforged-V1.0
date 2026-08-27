# CodeUpdate0284 — Borin Release Isolation

## Goal

Create a fail-closed public-release path that keeps the private Borin player/engineering system out of official Reforged binaries and packages.

## What changed

- `Reforged.App.csproj` now supports `-p:PublicRelease=true`.
- Public builds replace the private application composition root with `App.PublicRelease.cs`.
- Public builds replace the normal shell XAML with `MainWindow.PublicRelease.xaml`, which has no Borin navigation surface.
- Public builds remove the private Brains, BorinCapabilities, Forge, Engineering, Borin view/view-model, NVIDIA key UI, and Borin image resources from compilation/resources.
- LLamaSharp, Vulkan AI backend, and Roslyn coding-model dependencies are not restored for the public build.
- Shared client command ownership is now named `AutonomousAgent` instead of exposing Borin through the common client model/service layer.
- Saved workspace restoration now falls back to Home when a workspace from a developer build is unavailable in the public composition.

## Release gate

`Tools/Release/validate_public_release.py` provides two fail-closed gates:

1. Source isolation: checks that the public composition/exclusions exist and that the effective public source/resource set contains no Borin/private-provider identifiers.
2. Artifact isolation: scans the exact staging payload and final ZIP, including managed binaries/resources, for private identifiers, private paths, source/project files, scripts, symbols, and other developer artifacts.

The artifact gate checks both UTF-8/ASCII and UTF-16LE representations so managed metadata/string heaps cannot bypass a simple text-file scan.

## Build command

From the repository root on Windows:

```powershell
.\Tools\Release\build_public_release.ps1
```

The script:

1. validates the public source boundary;
2. recreates `Release/PublicRelease/Staging`;
3. publishes `Reforged.App` with `PublicRelease=true`;
4. copies only runtime/reference roots needed by the standalone product and strips development scripts/projects from the Librarium copy;
5. writes a minimal public `ProjectManifest.json` instead of shipping the development manifest;
6. scans the exact staging payload and writes SHA-256 hashes to `PUBLIC_RELEASE_MANIFEST.json`;
7. scans the final staging payload again;
8. creates `Release/PublicRelease/MajorMud-Reforged-Public-win-x64.zip`; and
9. scans the exact ZIP entries/bytes and CRC before accepting the package. If that final gate fails, the ZIP is deleted.

If any private identifier is found, packaging stops and no release ZIP is created.

## Development build behavior

Normal builds remain Borin-enabled. The private development composition, assets, AI providers, engineering surfaces, and existing Borin tests are unchanged except for the neutral shared command-origin enum name.

## Verification in this environment

- Public source isolation validator: PASS.
- Python syntax/static validation: PASS.
- Public artifact scan logic tested with clean and deliberately contaminated synthetic staging directories: PASS (clean accepted, contaminated rejected).
- Full `dotnet publish -p:PublicRelease=true`: NOT RUN here because the .NET SDK is unavailable in this environment. The Windows release script performs that build and scans the resulting binary before packaging.
