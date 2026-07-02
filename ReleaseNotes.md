# CodeUpdate-0051 - Import Staging and Build0021 Behavioral Recovery

This update creates a report-only import staging surface and preserves the user-provided Build0021 behavioral recovery evidence package.

Highlights:

- Added `Tools/DataDecode/Build-ImportStagingReport.ps1`.
- Generated `DataDecodeCheckpoints/ImportStaging/`.
- Added a canonical import staging queue and Android ChatGPT phone handoff template.
- Preserved Build0021 under `Docs/BehavioralRecovery/Build0021/`.
- Preserved Build0021 under `DataDecodeCheckpoints/SimulatorBuild0021/`.
- Added `Docs/CodeUpdates/CODEX_BUILD0021_REPO_UPDATE.md`.
- Added `Core/Runtime/BehavioralRecovery/README.md`.

Verification:

- CodeUpdate-0050 GitHub Actions is green by user report.
- Build0021 ZIP was inspected and copied overlay files were verified against the handoff manifest hashes.
- Import staging reports were generated and inspected.
- Local sequential Core and Core.Tests Release builds passed with 0 warnings and 0 errors.
- Local core/server smoke tests passed.

Known limitation:

- Build0021 is behavioral recovery evidence only. It uses no live Worldgroup runtime validation and does not certify `strong_candidate` or `runtime_required` formulas as original behavior.
- The outer Codex handoff manifest hashes matched copied Build0021 files. The internal Build0021 manifest contains stale rows for its own manifest and verification JSON, preserved as supplied evidence.

# CodeUpdate-0050 - SimulatorBuild0002 Handoff and DataDecode0110 Static Certification

This update preserves the user-provided SimulatorBuild0002 handoff and final static DataDecode0110 checkpoint.

Highlights:

- Added `DataDecodeCheckpoints/SimulatorBuild0002/`.
- Added `DataDecodeCheckpoints/DataDecode0110/`.
- Added selected DataDecode0099-0108 DAT decoding decision docs.
- Added `Tools/Simulator/MajorMUD-Reforged-SimulatorPrototype/`.
- Recorded that the simulator is expected-model evidence only, not live runtime certification.
- Excluded the embedded stale full repository archive and generated Python cache files.

Verification:

- Handoff ZIP contents were inspected.
- Handoff ZIP SHA-256 was recorded.
- DataDecode0110 CSV/JSON files parsed locally.
- Unified decoded-data inventory was regenerated.
- .NET Core/Core.Tests Release builds passed.
- Core/server smoke tests passed.
- Python 3.14.6 ran simulator summary, unknown queue, room search, spell search, and smoke test successfully.

Known limitation:

- SimulatorBuild0002 can help server work as a scenario/reference harness, but live runtime behavior still requires controlled Worldgroup validation.

# CodeUpdate-0049 - DataDecode0068 Spell Engine Proof

This update preserves the user-provided DataDecode0068 Spell Engine Proof checkpoint.

## Changed

- Added DataDecode0068 under `DataDecodeCheckpoints/DataDecode0068/`.
- Added a DataDecode0068 repository README.
- Added a CodeUpdate-0049 report.
- Refreshed `DataDecodeCheckpoints/UnifiedDecodedData/`.
- Recorded that DataDecode0051 through DataDecode0067 are not claimed as repo-preserved unless supplied later.

## Verification

- DataDecode0068 ZIP contents were listed and copied from its payload.
- DataDecode0068 ZIP SHA-256 was recorded.
- DataDecode0068 report, metrics, and handoff were inspected.
- Unified decoded-data report builder ran successfully.
- Final local verification is recorded in `UpdateManifest.json`.

## Boundaries

- No canonical `Data/` rewrite.
- No spell field promotions.
- No server behavior changes.
- No gameplay systems.
- No executable execution.
- No OmniVerse-only promotion.

## Previous Release Notes
# CodeUpdate-0048 - Unified Decoded Data and Phone Workflow

This update creates a unified decoded-data index and adds an Android ChatGPT handoff workflow.

## Changed

- Added `Tools/DataDecode/Build-UnifiedDecodedData.ps1`.
- Generated `DataDecodeCheckpoints/UnifiedDecodedData/`.
- Added category comparison reports against the CodeUpdate0046 OmniVerse/MudInfo reference payload.
- Added Android ChatGPT decode workflow docs and phone handoff templates.

## Verification

- Unified decoded-data report builder ran successfully.
- Generated manifest and comparison summary were inspected.
- Final local verification is recorded in `UpdateManifest.json`.

## Boundaries

- No canonical `Data/` rewrite.
- No field promotions.
- No server behavior changes.
- No gameplay systems.
- No imported/reference deletion.
- No OmniVerse-only promotion.

## Previous Release Notes
# CodeUpdate-0047 - DataDecode GitHub Transition

This update transitions decode work to the GitHub repository as the authoritative working copy.

## Changed

- Preserved DataDecode0049 under `DataDecodeCheckpoints/DataDecode0049/`.
- Recorded DataDecode0049 WGSERVER record access / structure correlation findings.
- Added DataDecode0050 GitHub transition handoff and metrics under `DataDecodeCheckpoints/DataDecode0050/`.
- Recorded future decode passes as repository-direct commits, with ZIPs limited to archival/checkpoint artifact use.

## Verification

- DataDecode0049 ZIP contents were listed and preserved.
- DataDecode0049 ZIP SHA-256 was recorded.
- `wgserver.exe` size and SHA-256 were recorded without copying or executing the binary.
- Final local verification is recorded in `UpdateManifest.json`.

## Boundaries

- No canonical `Data/` rewrite.
- No field promotions.
- No server behavior changes.
- No gameplay systems.
- No executable execution.
- No public-ID to binary-row assumptions.

## Previous Release Notes
# CodeUpdate-0046 - OmniVerse Capture Checkpoint

This update records the current OmniVerse/MudInfo reference capture state before continuing data gathering.

## Changed

- Recorded CodeUpdate-0045 as green by user report.
- Recorded `E:\Httracks\OmniVerseCodexFull` as the HTTrack OmniVerse SPA shell/assets capture.
- Imported the gathered OmniVerse/MudInfo reference payload under `ImportedDatabases/OmniVerseReference/CodeUpdate0046/`.
- Recorded WCC API capture counts and hashes for abilities, classes, items, monsters, races, shops, spells, world status, 168 shop-seeded roommaps, 1,246 room details, and a 23-entry room capture error log.
- Recorded and preserved MudInfo `maps.rar` hash, source archive, and 102 extracted MudInfo map files.

## Verification

- Local reference capture files were counted and hashed.
- HTTrack limitation was verified: the site uses API-loaded data, so HTTrack does not enumerate full codex records by itself.
- Final local repository verification is recorded in `UpdateManifest.json`.

## Boundaries

- Captured reference evidence was copied into `ImportedDatabases/OmniVerseReference/CodeUpdate0046/` for validation/correlation only.
- No canonical `Data/` rewrite.
- No field promotions.
- No server behavior changes.
- No gameplay systems.
- No public-ID to binary-row assumptions.

## Previous Release Notes
# CodeUpdate-0045 - DataDecode0043 Checkpoint Integration

This update integrates the user-provided DataDecode0043 Import Readiness Validation checkpoint as repository reference documentation.

## Changed

- Added `DataDecodeCheckpoints/DataDecode0043/`.
- Preserved the DataDecode0043 report, handoff, metrics, manifest, schema audits, repository JSON audits, foreign-key import gate review, import blocker register, staging decision matrix, native source integrity report, and append notes.
- Recorded DataDecode0043 as the pre-mirror import-readiness baseline.
- Recorded that no canonical fields were promoted.
- Recorded package manifest limitations for `DataDecode0043_Manifest.csv` and checkpoint `UpdateManifest.json`.

## Verification

- ZIP SHA-256: `0547361D07D8979F77F88B2D463E9AB720FC9851831AE107C369189FF51A5259`.
- ZIP contents listed and extracted into `DataDecodeCheckpoints/DataDecode0043/`.
- 18 of 20 checkpoint manifest-listed files matched recorded size/hash.
- 2 manifest-listed files had stale recorded size/hash entries and are documented as known package limitations.
- Final local verification is recorded in `UpdateManifest.json`.

## Boundaries

- No canonical `Data/` rewrite.
- No imported data deletion.
- No field promotions.
- No server behavior changes.
- No gameplay systems.
- No public-ID to binary-row assumptions.
- No exact MajorMUD parity claim.

## Previous Release Notes
# CodeUpdate-0044 - Original Reference Extractor

This update adds a safe reference-capture extractor for original/source material. It preserves and inventories legacy files for later reviewed extraction work without running original programs or changing live Reforged `Data/`.

## Changed

- Added `Tools/ReferenceCapture/Capture-OriginalReferenceData.ps1`.
- Added `Tools/ReferenceCapture/README.md`.
- Added archive preservation under named `ImportedDatabases/ReferenceCaptures/` output folders.
- Added file inventory, SHA-256 hashing, category hints, printable string extraction, and JSON/CSV/Markdown reports under named `Reports/ReferenceCaptures/` output folders.
- Supports `.zip` extraction directly and `.rar`/`.7z` extraction when 7-Zip is installed.
- Recorded CodeUpdate-0043 GitHub Actions as green by user report.

## Verification

- PowerShell extractor self-test passed against `Tools/ReferenceCapture/README.md`.
- Self-test produced one file inventory and 35 string rows.
- Temporary self-test output was removed after inspection.
- Final .NET verification is recorded in `UpdateManifest.json`.

## Boundaries

- No canonical `Data/` rewrite.
- No imported data deletion.
- No original EXE, DLL, COM, or SYS execution.
- No keygen use or license bypass.
- No WCC/WBB/WSGSERVE automatic reconciliation.
- No gameplay systems.
- No world simulation loop.
- No exact MajorMUD parity claim.

## Previous Release Notes
# CodeUpdate-0043 - Server Data Readiness Classification

This update separates canonical data file presence from runtime readiness. Server startup now reports which data categories are runtime-ready and which are present but blocked by empty placeholder data.

## Changed

- Added runtime-ready and runtime-blocked counts.
- Added placeholder classification for empty canonical categories.
- Added readiness notes for each Server data category.
- Added smoke-test coverage for the readiness classification.
- Recorded the eventual goal to retire imported/reference data after Reforged-native data is complete and verified.

## Verification

- Server bootstrap reports 13/13 categories present.
- Runtime-ready categories: 8.
- Runtime-blocked placeholder categories: 5.
- Loaded records: 4,527.
- Gameplay remains disabled.

## Boundaries

- No canonical `Data/` rewrite.
- No imported data deletion.
- No WCC import or reconciliation.
- No gameplay systems.
- No world simulation loop.
- No exact MajorMUD parity claim.

## Previous Release Notes
# CodeUpdate-0042 - Server Data Catalog Bootstrap

This update adds read-only canonical Server data catalog loading to the non-gameplay bootstrap path. Server startup now loads registered Core `Data/` categories and prints category readiness before any gameplay systems are started.

## Changed

- Added Server data catalog audit models and loader.
- Attached canonical data category readiness to Server bootstrap.
- Printed category status, file size, and loaded record count during startup.
- Added smoke-test coverage.

## Verification

- Server bootstrap loads 13/13 categories and 4,527 records.
- Empty placeholder categories remain visible: loot tables, quests, rooms, shops, and trainers.
- Gameplay remains disabled.

## Boundaries

- No canonical `Data/` rewrite.
- No WCC import or reconciliation.
- No gameplay systems.
- No world simulation loop.
- No exact MajorMUD parity claim.

## Previous Release Notes
# CodeUpdate-0041 - Classic Parity Audit Foundation

This update adds the first read-only Classic parity audit foundation. Server bootstrap now inventories canonical `Data/` and imported reference material so future updates can measure Classic coverage after exact parsers are added.

## Changed

- Added Classic parity audit models and inventory logic.
- Attached Classic parity inventory to Server bootstrap.
- Added Server startup output for broad parity categories.
- Added smoke-test coverage.
- Recorded that all WCC data is reference-only unless explicitly approved otherwise.

## Boundaries

- No canonical `Data/` rewrite.
- No `ImportedDatabases/` overwrite.
- No gameplay systems.
- No exact MajorMUD parity claim.
- No proprietary binary parser yet.

## Previous Release Notes
# CodeUpdate-0039 - Server Bootstrap Readiness Summary

This update improves non-gameplay Server bootstrap diagnostics for future host/editor tooling.

## Changed

- Server bootstrap reports now expose a compact readiness summary.
- Readiness includes project root, required directories, manifest identity, startup diagnostics, and host-tool readiness.
- Server startup output now prints readiness status.
- Gameplay status remains explicitly disabled.
- Smoke tests verify readiness status on the successful bootstrap path.
- Classic-first Server direction is recorded for future planning: Classic baseline first, mode-ready architecture later.
- Engine-first architecture direction is recorded: reusable engine systems over feature-specific server code.
- User-provided WCC default install is preserved under `ImportedDatabases/WCCDefaultInstall/` as reference/import material.
- CodeUpdate-0038 is recorded as green by user report.

## Boundaries

- No Reforged `Data/` files are rewritten.
- No `ImportedDatabases/` files are overwritten.
- No gameplay systems are added.
- No combat is added.
- No character creation is added.
- No world simulation loop is added.
- No networking expansion is added.
- No live editor implementation is added.
- No Reforged mode, Custom mode, AI systems, alternate gameplay rules, or mode/profile loader is implemented.
- No canonical `Data/` rewrite is included.
- No WCC executable/runtime file is made a live server dependency.

## Verification

- Local .NET 8.0.422 JSON parse, affected Release builds, smoke tests, Server bootstrap run, and WCC default archive integrity verification passed after folded documentation/import work.
- GitHub Actions remains pending until the user pushes this update and reports the result.

## Previous Release Notes
# CodeUpdate-0038 - Server Bootstrap Directory Status

This update improves non-gameplay Server bootstrap diagnostics for future host tooling.

## Changed

- Server bootstrap reports now expose required-directory status records.
- Server bootstrap reports now expose required-directory total, present, and missing counts.
- Server startup output now prints required-directory summary and detail lines.
- Smoke tests verify required-directory status on the successful bootstrap path.
- CodeUpdate-0037 is recorded as green by user report.

## Boundaries

- No Reforged `Data/` files are rewritten.
- No `ImportedDatabases/` files are overwritten.
- No gameplay systems are added.
- No combat is added.
- No character creation is added.
- No world simulation loop is added.
- No networking expansion is added.
- No live editor implementation is added.

## Verification

- Local .NET 8.0.422 JSON parse, restore, sequential Release build, smoke tests, and Server bootstrap run passed.
- GitHub Actions is green by user report.

## Previous Release Notes
# CodeUpdate-0037 - Software Design Specification and Continuity Cleanup

This documentation-only update combines the CodeUpdate-0036B continuity cleanup package with the CodeUpdate-0037 Software Design Specification package.

## Changed

- Fetched GitHub truth and fast-forwarded local `main` to the CodeUpdate-0036 baseline before applying.
- Repaired stale continuity documents that still referenced CodeUpdate-0035 after CodeUpdate-0036 direct-GitHub documentation commits.
- Added draft Software Design Specification planning records under `Docs/SDS/`.
- Added CodeUpdate-0037 documentation and verification report.

## Boundaries

- No source-code behavior is changed.
- No Reforged `Data/` files are rewritten.
- No `ImportedDatabases/` files are overwritten.
- No gameplay systems are added.
- No combat is added.
- No character creation is added.
- No world simulation loop is added.
- No networking expansion is added.
- No live editor implementation is added.

## Verification

- CodeUpdate-0036B and CodeUpdate-0037 ZIP contents were inspected before merging.
- JSON parse and local .NET 8.0.422 restore/build/smoke verification passed.
- Initial parallel Client/Editor build attempts hit a transient shared Core `obj` file lock; rerunning those builds sequentially passed with 0 warnings and 0 errors.
- GitHub Actions is green by user report.

## Previous Release Notes
# CodeUpdate-0035 - Server Bootstrap Validation Summary

This update improves non-gameplay Server bootstrap diagnostics for host tooling.

## Changed

- Server bootstrap reports now expose validation issue counts.
- Server startup output now prints a validation summary by severity.
- Smoke tests verify issue-count behavior for success and failure paths.
- CodeUpdate-0034 is recorded as green by user report.

## Boundaries

- No Reforged `Data/` files are rewritten.
- No `ImportedDatabases/` files are overwritten.
- No gameplay systems are added.
- No combat is added.
- No character creation is added.
- No world simulation loop is added.
- No networking expansion is added.
- No live editor implementation is added.

## Verification

- Local .NET 8.0.422 restore/build/smoke verification passed.
- GitHub Actions remains the official validation gate after the user pushes this update.

## Previous Release Notes
# CodeUpdate-0034 - OmniVerse Reference Audit Plan

This update records OmniVerse as a read-only comparison source for future data audits.

## Changed

- Documented OmniVerse public reference endpoints verified during discovery.
- Added a reference-source policy: OmniVerse can help identify mismatches, but it cannot overwrite Reforged `Data/`.
- Recorded CodeUpdate-0033 as green by user report.
- Added a 0034 audit plan and report.

## Boundaries

- No Reforged `Data/` files are rewritten.
- No `ImportedDatabases/` files are overwritten.
- No gameplay systems are added.
- No combat is added.
- No character creation is added.
- No world simulation loop is added.
- No networking expansion is added.
- No live editor implementation is added.

## Verification

- Local .NET 8.0.422 restore/build/smoke verification passed.
- No files under `Data/` or `ImportedDatabases/` were changed.
- GitHub Actions is green by user report.

## Previous Release Notes
# CodeUpdate-0033 - Server Bootstrap Exit Code Contract

This update improves the non-gameplay Server bootstrap contract for automation and diagnostics.

## Changed

- Server bootstrap reports now expose an explicit exit code.
- Server startup returns the report exit code on success and failure.
- Smoke tests verify success returns exit code 0 and failure returns exit code 1.
- Continuity documents record CodeUpdate-0032 as green by user report.

## Boundaries

- No gameplay systems are added.
- No combat is added.
- No character creation is added.
- No world simulation loop is added.
- No networking expansion is added.
- No live editor implementation is added.

## Verification

- Local .NET 8.0.422 restore/build/smoke verification passed.
- GitHub Actions is green by user report.

## Previous Release Notes
# CodeUpdate-0032 - Server Bootstrap Manifest Identity

This update improves the non-gameplay Server bootstrap diagnostics.

## Changed

- Server bootstrap report now captures ProjectManifest identity fields.
- Server bootstrap console output now prints project, CodeUpdate, database version, and milestone.
- Smoke tests now verify those manifest identity fields are captured.
- Continuity documents record the 0032 scope and next handoff rule.

## Handoff Improvement

Future updates should include a short next-scope candidate and explicit boundaries separately from completed work. This makes handoff safer without starting unapproved work.

## Boundaries

- No gameplay systems are added.
- No combat is added.
- No character creation is added.
- No world simulation loop is added.
- No networking expansion is added.
- No live editor implementation is added.

## Verification

- Local .NET 8.0.422 restore/build/smoke verification passed.
- GitHub Actions is green by user report.

## Previous Release Notes
# CodeUpdate-0031A - Server Smoke Test Compile Fix

This update is a corrective build-fix patch for CodeUpdate-0031.

## Fixed

- Corrected the configuration smoke-test JSON string in `Tests/Source/Reforged.Core.Tests/Program.cs`.
- The previous CodeUpdate-0031 file contained unescaped JSON quotes inside a normal C# string literal, causing GitHub Actions to fail with `Syntax error, ',' expected` at `Program.cs#L59`.

## Boundaries

- No gameplay systems are added.
- No combat is added.
- No character creation is added.
- No world simulation loop is added.
- No networking expansion is added.
- No live editor implementation is added.

## Verification

- GitHub truth was read before creating this patch.
- The user-provided GitHub Actions failure output was matched to the current GitHub file.
- The update ZIP was verified for one top-level folder, BAT installer, manifest, and patch-only payload before delivery.
- The ChatGPT packaging container did not include the .NET SDK, so local restore/build/test could not be run during ZIP creation.
- The user later reported CodeUpdate-0031A GitHub Actions green.
- Codex local .NET 8.0.422 sequential restore/build/smoke verification passed after the patch was applied locally.
