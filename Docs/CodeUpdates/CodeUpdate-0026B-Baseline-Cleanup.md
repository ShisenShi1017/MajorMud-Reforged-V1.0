# CodeUpdate-0026B - Baseline Cleanup

## Purpose

Clean the Milestone 1 finalization candidate before freezing the baseline.

This update removes generated build artifacts from the source workspace, repairs ignore rules, and fixes visible documentation encoding damage. It does not add gameplay features.

## Changes

- Removed generated source-project `bin/` and `obj/` folders under `Apps/Source/`, `Core/Source/`, and `Tests/Source/`.
- Preserved the intentional root `Bin/` runtime handoff folder.
- Added targeted `.gitignore` rules for source/test project build outputs.
- Repaired visible mojibake in README, changelog, and roadmap continuity files.
- Updated continuity documents and manifests for CodeUpdate-0026B.

## Verification

Local .NET 8.0.422 restore/build/smoke-test verification passed for Core, Server, Importer, Editor, Launcher, Client, and the Core smoke-test project.

The first parallel build attempt hit a local file-lock on the shared Core output because several projects rebuilt Core at the same time. Sequential verification passed cleanly afterward.

## Official Gate

The user later reported CodeUpdate-0026B GitHub Actions green. CodeUpdate-0027 records the final Milestone 1 baseline freeze.
