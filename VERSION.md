# Reforged Version

- Project Version: 0.1.0
- Current Code Update: 0050
- Current Database Version: 0009
- Milestone: Milestone 2 - Engine Modernization / Host & World Editor Planning
- Runtime Requirement: No .NET SDK required for normal runtime. Run from compiled/runtime-ready artifacts in `Bin/`.
- Build Verification: GitHub Actions cloud build via `.github/workflows/dotnet-build.yml` using `Reforged.*` project paths.

## Verification Status

- Current update: CodeUpdate-0050 - SimulatorBuild0002 Handoff and DataDecode0110 Static Certification
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
