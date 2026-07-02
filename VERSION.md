# Reforged Version

- Project Version: 0.1.0
- Current Code Update: 0054
- Current Database Version: 0009
- Milestone: Milestone 2 - Engine Modernization / Host & World Editor Planning
- Runtime Requirement: No .NET SDK required for normal runtime. Run from compiled/runtime-ready artifacts in `Bin/`.
- Build Verification: GitHub Actions cloud build via `.github/workflows/dotnet-build.yml` using `Reforged.*` project paths.

## Verification Status

- Current update: CodeUpdate-0054 - Validation0013 and DocUpdate0024 New-Files Intake
- Milestone 1 Core Framework is complete/frozen.
- CodeUpdate-0031 added a non-gameplay Server bootstrap entry point, bootstrap validation, and smoke coverage.
- CodeUpdate-0031 failed GitHub Actions in the test build due to invalid C# string escaping in `Tests/Source/Reforged.Core.Tests/Program.cs`.
- CodeUpdate-0031A fixes the syntax error only.
- CodeUpdate-0031A GitHub Actions is green by user report.
- Codex local .NET 8.0.422 sequential restore/build/smoke verification passed.
- CodeUpdate-0032 local .NET 8.0.422 restore/build/smoke verification passed.
- CodeUpdate-0032 GitHub Actions is green by user report.
- CodeUpdate-0033 local .NET 8.0.422 restore/build/smoke verification passed.
- CodeUpdate-0033 GitHub Actions is green by user report.
- CodeUpdate-0034 local .NET 8.0.422 restore/build/smoke verification passed.
- CodeUpdate-0034 GitHub Actions is green by user report.
- CodeUpdate-0035 local .NET 8.0.422 restore/build/smoke verification passed.
- CodeUpdate-0035 GitHub Actions is green by user report.
- CodeUpdate-0036 is documentation/research only and is present in GitHub `origin/main` at commit `105ed76b`.
- CodeUpdate-0037 is documentation-only; it combines the CodeUpdate-0036B continuity cleanup package with the CodeUpdate-0037 Software Design Specification package.
- CodeUpdate-0037 JSON parse and local .NET 8.0.422 restore/build/smoke verification passed.
- CodeUpdate-0037 GitHub Actions is green by user report.
- CodeUpdate-0038 adds required-directory status reporting to the non-gameplay Server bootstrap path.
- CodeUpdate-0038 local .NET 8.0.422 JSON parse, restore, sequential Release build, smoke tests, and Server bootstrap run passed.
- CodeUpdate-0038 GitHub Actions is green by user report.
- CodeUpdate-0039 adds a host-tool readiness summary while keeping gameplay disabled.
- CodeUpdate-0039 records the Classic-first Server direction as planning/continuity documentation.
- CodeUpdate-0039 records engine-first architecture direction as planning/continuity documentation.
- CodeUpdate-0039 adds the user-provided WCC default install under `ImportedDatabases/WCCDefaultInstall/` as reference/import material only.
- CodeUpdate-0039 local .NET 8.0.422 JSON parse, affected Release builds, smoke tests, Server bootstrap run, and WCC default archive integrity verification passed after folded documentation/import work.
- CodeUpdate-0040 preserves WCC-labeled WBB material under `ImportedDatabases/WCCLabeledWBB/` as reference/import material only.
- CodeUpdate-0041 adds a read-only Classic parity audit foundation attached to Server bootstrap.
- CodeUpdate-0041 does not parse binary legacy formats yet and does not claim exact MajorMUD parity.
- CodeUpdate-0042 adds read-only canonical Server data catalog loading to Server bootstrap.
- CodeUpdate-0042 local bootstrap loads 13/13 registered categories and 4,527 records, with 5 empty placeholder categories.
- CodeUpdate-0043 classifies canonical Server data readiness as runtime-ready or runtime-blocked while keeping startup non-gameplay.
- CodeUpdate-0043 local bootstrap reports 8 runtime-ready categories, 5 runtime-blocked placeholders, and 4,527 loaded records.
- CodeUpdate-0043 GitHub Actions is green by user report.
- CodeUpdate-0044 adds a reference-only original data capture/extractor tool under `Tools/ReferenceCapture/`.
- CodeUpdate-0044 local PowerShell extractor self-test passed against `Tools/ReferenceCapture/README.md`; temporary self-test outputs were removed.
- CodeUpdate-0044 does not run original executables, does not use keygens, does not rewrite canonical `Data/`, and does not add gameplay.
- CodeUpdate-0045 integrates the DataDecode0043 Import Readiness Validation checkpoint under `DataDecodeCheckpoints/DataDecode0043/`.
- DataDecode0043 reports approximately 96-97% static decode completion, 5 import blockers, and 0 canonical field promotions.
- CodeUpdate-0045 does not change canonical `Data/`, server behavior, gameplay, or import state.
- CodeUpdate-0045 GitHub Actions is green by user report.
- CodeUpdate-0046 records HTTrack OmniVerse shell/assets capture externally and imports the gathered OmniVerse/MudInfo API/map reference payload under `ImportedDatabases/OmniVerseReference/CodeUpdate0046/`.
- CodeUpdate-0046 does not rewrite canonical `Data/`, change server behavior, promote fields, add gameplay systems, or assume OmniVerse public IDs equal binary row indexes.
- CodeUpdate-0047 preserves DataDecode0049 under `DataDecodeCheckpoints/DataDecode0049/`, records DataDecode0050 GitHub transition handoff under `DataDecodeCheckpoints/DataDecode0050/`, and moves future decode workflow to repository-direct commits.
- CodeUpdate-0047 does not rewrite canonical `Data/`, change server behavior, promote fields, add gameplay systems, execute `wgserver.exe`, or assume OmniVerse public IDs equal binary row indexes.
- CodeUpdate-0048 adds `DataDecodeCheckpoints/UnifiedDecodedData/` with generated decoded-data inventory and OmniVerse comparison reports.
- CodeUpdate-0048 adds Android ChatGPT decode workflow docs and phone handoff templates for non-PowerShell phone-based decode work.
- CodeUpdate-0048 does not rewrite canonical `Data/`, change server behavior, promote fields, add gameplay systems, or delete imported/reference data.
- CodeUpdate-0049 preserves DataDecode0068 Spell Engine Proof under `DataDecodeCheckpoints/DataDecode0068/` and refreshes the unified decoded-data inventory.
- CodeUpdate-0049 records 1,151 canonical spell `sourceId` to OmniVerse `legacy_number` matches and 0 field promotions.
- CodeUpdate-0049 does not rewrite canonical `Data/`, change server behavior, promote spell fields, add gameplay systems, execute `wgserver.exe`, or make OmniVerse-only promotions.
- CodeUpdate-0050 preserves SimulatorBuild0002 handoff evidence, extracts DataDecode0110 final static certification, and installs the expected-model simulator under `Tools/Simulator/`.
- CodeUpdate-0050 records DataDecode0110 as static `ImportedDatabases` certification only, with runtime validation still not started.
- CodeUpdate-0050 does not rewrite canonical `Data/`, change server behavior, promote fields, add gameplay systems, certify live runtime behavior, or commit the embedded stale full repository archive.
- CodeUpdate-0050 GitHub Actions is green by user report.
- CodeUpdate-0051 adds report-only import staging readiness reports under `DataDecodeCheckpoints/ImportStaging/`.
- CodeUpdate-0051 preserves Build0021 behavioral recovery evidence under `Docs/BehavioralRecovery/Build0021/` and `DataDecodeCheckpoints/SimulatorBuild0021/`.
- Build0021 records 6 `verified_from_data`, 4 `strong_candidate`, 2 `weak_candidate`, and 4 `runtime_required` formula/catalog entries.
- CodeUpdate-0051 local JSON/CSV/report validation passed.
- CodeUpdate-0051 local sequential Core and Core.Tests Release builds passed with 0 warnings and 0 errors.
- CodeUpdate-0051 local core/server smoke tests passed.
- CodeUpdate-0051 does not rewrite canonical `Data/`, change server behavior, promote fields, add gameplay systems, certify live runtime behavior, or certify Build0021 candidate formulas.
- CodeUpdate-0051 GitHub Actions is green by user report.
- CodeUpdate-0052 preserves Build0027 current-state handoff evidence under `DataDecodeCheckpoints/CurrentStateBuild0027/` and `Docs/BehavioralRecovery/Build0027/`.
- CodeUpdate-0052 records that Build0024/0025 matrices remain `expected_model` until converted into actual executable simulator tests.
- CodeUpdate-0052 records that prior chat pass/fail claims are not authoritative unless backed by artifact logs or locally generated test output.
- CodeUpdate-0052 local JSON/CSV/report validation passed.
- CodeUpdate-0052 local sequential Core and Core.Tests Release builds passed with 0 warnings and 0 errors.
- CodeUpdate-0052 local core/server smoke tests passed.
- CodeUpdate-0052 does not add executable harness code, rewrite canonical `Data/`, change server behavior, promote fields, add gameplay systems, or certify original runtime behavior.
- CodeUpdate-0052 GitHub Actions is green by user report.
- CodeUpdate-0053 replaces only repository `Data/` from the embedded Patch0068 `/Data` payload.
- CodeUpdate-0053 preserves Patch0068 findings once under `DataDecodeCheckpoints/Patch0068/`.
- CodeUpdate-0053 preserves Handoff0074 once under `DataDecodeCheckpoints/Handoff0074/`.
- CodeUpdate-0053 records Patch0071 as unreadable by the built-in ZIP reader and not applied.
- CodeUpdate-0053 updates server data-readiness diagnostics and smoke expectations so populated Patch0068 data categories are no longer treated as empty placeholders.
- CodeUpdate-0053 does not apply non-`Data` Patch0068 files, does not add gameplay server behavior, does not add gameplay systems, and does not implement Patch0074 master verification.
- CodeUpdate-0053 GitHub Actions is green by user report.
- CodeUpdate-0054 copies only repository-new files from Validation0013 and DocUpdate0024.
- CodeUpdate-0054 copies 437 new Validation0013 files, skips 86 existing Validation0013 paths, copies 2 new DocUpdate0024 files, and overwrites no existing repository files.
- CodeUpdate-0054 preserves the CodeUpdate-0053 `Data/` baseline and does not apply any existing-path cumulative package overlay.

## Reference Data Policy

- Reforged `Data/` remains canonical.
- `ImportedDatabases/` remains archive/reference only.
- All WCC data, including WCC default installs, WCC-labeled WBB material, WSGSERVE-related files, and WCC-derived archives, is reference-only unless the user explicitly approves a reviewed import/reconciliation update.
- `ImportedDatabases/` should be retired only after all needed source data has been decoded, imported, recreated as Reforged-native `Data/`, verified, and explicitly approved for cleanup.
- OmniVerse may be used as a read-only comparison source to identify possible corrections.
- LLT XML, MegaMUD databases, MegaMUD `.mp` paths, Mud Hex documentation, MudInfo, MudCentral, Reddit/community sources, WCC archives, and WSGSERVE `.dat` files are reference or verification sources only unless explicitly approved for a reviewed import/reconciliation update.
- No website/API/community/tool data may overwrite Reforged data without a reviewed, approved follow-up update.

## Source of Truth

- GitHub repository `ShisenShi1017/MajorMud-Reforged-V1.0` is the authoritative source of truth.
- Old ZIPs, prior generated patches, and conversation memory are not authoritative when they conflict with GitHub.

## Work Mode Rule

- Codex mode updates local only and waits for the user to push and state that GitHub Actions is green before continuing.
- ChatGPT mode uses patch ZIPs for local application unless the user explicitly requests direct GitHub edits.

## Update Packaging

- ChatGPT-created CodeUpdate ZIPs are patch-only packages, not full repository rebuilds, unless explicitly requested.
- Each ChatGPT-created update ZIP must contain exactly one top-level folder and an included `Apply-CodeUpdate-XXXX.bat` installer.
- Each ChatGPT-created update ZIP must include `UpdateManifest.json`.
- The ZIP must be physically created and inspected before it is announced.

## Never Guess Rule

- Never guess when creating, fixing, reviewing, or reporting an update.
- Verify current file paths, APIs, package contents, build status, milestone status, and update status before claiming success.
- If something cannot be verified in the current environment, say so clearly.
