# CodeUpdate-0026B - Baseline Cleanup Report

## Summary

CodeUpdate-0026B cleans the Milestone 1 finalization candidate before baseline freeze.

## Cleanup Results

- Generated lowercase `bin/` and `obj/` folders under source/test projects were removed from the local workspace.
- Root `Bin/` was preserved because it is the planned runtime artifact handoff location.
- `.gitignore` now ignores source/test project build outputs without hiding root `Bin/`.
- Visible mojibake in active continuity documents was repaired.

## Local Verification

Local .NET 8.0.422 restore/build/smoke-test verification passed for Core, Server, Importer, Editor, Launcher, Client, and the Core smoke-test project. The smoke-test run reported all Milestone 1 core smoke tests passed.

## Freeze Status

The user later reported CodeUpdate-0026B GitHub Actions green. CodeUpdate-0027 records the final Milestone 1 baseline freeze.
