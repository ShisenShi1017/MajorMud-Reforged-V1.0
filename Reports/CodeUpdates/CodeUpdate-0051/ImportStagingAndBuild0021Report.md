# CodeUpdate-0051 Import Staging and Build0021 Behavioral Recovery Report

## Purpose

CodeUpdate-0051 creates a report-only import staging surface and preserves the user-provided Build0021 behavioral recovery evidence package.

This update does not rewrite canonical `Data/`, promote fields, change server behavior, add gameplay, or certify live Worldgroup runtime behavior.

## Import Staging Work

Added `Tools/DataDecode/Build-ImportStagingReport.ps1`.

Generated `DataDecodeCheckpoints/ImportStaging/` with:

- `Summary/CanonicalDataImportSurface.csv`
- `Summary/SimulatorEvidenceSurface.csv`
- `Queues/CanonicalImportStagingQueue.csv`
- `Queues/PhoneDecodeImportHandoffTemplate.csv`
- `Reports/CodeUpdate-0051-ImportStagingReadinessReport.md`
- `ImportStagingManifest.json`
- `README.md`

The staging queue records the current import surface:

- `Data/rooms.json`: 0 canonical records; 1,246 evidence-backed room records.
- `Data/shops.json`: 0 canonical records; 178 evidence-backed shop records.
- `Data/loot_tables.json`: 0 canonical records; 732 monster item drop edges.
- `Data/quests.json`: 0 canonical records; 125 quest/progression candidates.
- `Data/trainers.json`: 0 canonical records; 46 skill/trainer-related rows.
- `Data/items.json`: 1,770 canonical records; reconciliation required before rewrite.
- `Data/spells.json`: 1,180 canonical records; reconciliation required before rewrite.
- `Data/monsters.json`: 98 canonical records; 1,100 expected monster records need reviewed reconciliation.

## Build0021 Work

Inspected `MajorMUD-Reforged-CodexHandoff-Build0021-RepoUpdate.zip`.

Source ZIP SHA-256: `A7F88E72CEB8020F54666BC72989A0DB4E5294C806BB3C7265E93ABBA955BE36`.

Preserved its `repo_overlay/` payload under:

- `Docs/BehavioralRecovery/Build0021/`
- `DataDecodeCheckpoints/SimulatorBuild0021/`
- `Docs/CodeUpdates/CODEX_BUILD0021_REPO_UPDATE.md`
- `Core/Runtime/BehavioralRecovery/README.md`

Build0021 records:

- 6 `verified_from_data` formula/catalog entries.
- 4 `strong_candidate` entries.
- 2 `weak_candidate` entries.
- 4 `runtime_required` entries.
- Runtime used: no.
- Live Worldgroup validation: no.
- Simulator executable formula updates certified: none.

## Verification

- CodeUpdate-0050 GitHub Actions is green by user report.
- Build0021 ZIP was inspected before applying.
- Build0021 copied files match handoff manifest hashes.
- `Build-ImportStagingReport.ps1` ran successfully.
- Generated import staging queue was inspected.
- JSON and CSV parsing passed for the new manifests/reports checked during the update.
- Sequential local Core and Core.Tests Release builds passed with 0 warnings and 0 errors.
- Core/server smoke tests passed.

Known verification limitation:

- The outer Codex handoff manifest hashes match the copied Build0021 files.
- The internal `DataDecodeCheckpoints/SimulatorBuild0021/SIMULATOR_BUILD0021_MANIFEST.csv` contains stale rows for `SIMULATOR_BUILD0021_MANIFEST.csv` and `SIMULATOR_BUILD0021_VERIFICATION.json`.
- Those internal rows are preserved as supplied evidence and are not corrected in-place.

## Boundaries

- No `Data/` rewrite.
- No imported/reference deletion.
- No field promotion.
- No gameplay systems.
- No server behavior change.
- No live Worldgroup runtime validation claim.
- No OmniVerse-only promotion.
- No assumption that public IDs equal binary record indexes.
- No `strong_candidate` formula is certified as original behavior.
- No `runtime_required` behavior is promoted.

## Next Recommended Step

After user push and green GitHub Actions, create the first reviewed import diff for one high-confidence category, preferably rooms/navigation, while keeping hidden/scripted exits and runtime-only behavior marked uncertain.
