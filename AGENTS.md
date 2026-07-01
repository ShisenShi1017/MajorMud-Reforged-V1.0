# Reforged Agent Instructions

These instructions are mandatory for every AI assistant, Codex session, ChatGPT session, and developer handoff working on this repository.

## 1. Identify the work mode first

Before changing anything, identify which environment is being used.

### Codex mode

Use Codex mode only when the assistant is operating inside a Codex/local-repository coding environment.

Rules:

- Codex updates the local repository only.
- Codex does not create ZIP-based local handoff packages unless the user explicitly asks.
- Codex must wait after each update until the user pushes to GitHub and states that the current push has a green GitHub Actions result.
- Codex must not continue to the next Code Update until the current pushed update is green or the user explicitly overrides the gate.
- Codex must update continuity documents before ending the update.

### ChatGPT mode

Use ChatGPT mode when the assistant is operating in a regular ChatGPT chat window without direct local filesystem access.

Rules:

- ChatGPT cannot directly update the user's local repository.
- ChatGPT must work through local ZIP patch uploads unless the user explicitly chooses direct GitHub edits.
- ChatGPT update ZIPs must be patch-only packages, not rebuilt full repositories.
- Every ChatGPT update ZIP must contain exactly one named top-level folder.
- Every ChatGPT update ZIP must include an `Apply-CodeUpdate-XXXX.bat` installer and `UpdateManifest.json`.
- ChatGPT must verify the ZIP exists and has the required structure before announcing it.

## 2. Source of truth

The GitHub repository is the authoritative source of truth:

`ShisenShi1017/MajorMud-Reforged-V1.0`

Rules:

- Read the current GitHub repository state before making any update plan.
- Do not use old ZIP files, generated patches, or prior conversation memory as truth when they conflict with GitHub.
- If a local ZIP is used in ChatGPT mode, treat it only as a user-provided local-application vehicle. GitHub remains the truth after the user pushes.
- Do not continue from an unverified assumption.

## 3. Never guess

Accuracy is required.

Rules:

- Never guess method names, file paths, project state, build state, package contents, current update number, or milestone status.
- Verify before claiming something exists, passed, failed, or was changed.
- If something cannot be verified in the current environment, state exactly what was verified and exactly what remains unverified.
- Do not invent missing code, files, reports, or build results.
- Do not claim GitHub Actions is green unless GitHub or the user explicitly confirms the current pushed commit is green.

## 4. Required startup order

Every new work session must read or verify:

1. `AGENTS.md`
2. `AI_CONTEXT.md`
3. `Docs/Architecture/PROJECT_ROADMAP.md`
4. `README.md`
5. `VERSION.md`
6. `CHANGELOG.md`
7. `ProjectManifest.json`
8. `DEVELOPMENT_PRINCIPLES.md` when architecture, runtime, or code quality decisions are involved
9. Relevant app/source/test files for the requested change
10. Current GitHub Actions status when build verification is part of the task

## 5. Update rules

Every Code Update must:

- Confirm the current Code Update number from repository files.
- Confirm the current milestone from repository files.
- Confirm the current Database Version from repository files.
- Make the smallest safe change that satisfies the approved scope.
- Avoid gameplay work unless the user explicitly approves it.
- Preserve `Saves/`.
- Preserve `ImportedDatabases/` unless the user explicitly approves otherwise.
- Update continuity documents.
- Record verification status and known limitations.

## 6. Documentation continuity

Every Code Update must update all relevant continuity documents, including at minimum:

- `AGENTS.md` when workflow rules or current project position change
- `AI_CONTEXT.md`
- `README.md`
- `CHANGELOG.md`
- `VERSION.md`
- `ProjectManifest.json`
- `ReleaseNotes.md`
- `Docs/Architecture/PROJECT_ROADMAP.md`
- Any affected app README or report

Do not leave stale milestone, update, roadmap, source-of-truth, or next-step text behind.

## 7. Build and verification rules

- GitHub Actions is the official build gate after a push.
- Local build/test results are useful but do not replace GitHub Actions.
- Do not say an update is complete until the required local/package verification has been done.
- Do not say a pushed update is accepted until the user or GitHub status confirms green Actions for that push.

## 8. Packaging rules for ChatGPT ZIP updates

Every ChatGPT-created Code Update ZIP must:

- Be patch-only.
- Contain exactly one top-level folder.
- Include `Apply-CodeUpdate-XXXX.bat` inside that folder.
- Include `UpdateManifest.json` inside that folder.
- Include changed/new files under a `Payload/` folder using repository-relative paths.
- Automatically locate the repository root.
- Back up replaced files before copying when practical.
- Never overwrite `Saves/` or `ImportedDatabases/`.
- Be physically created and inspected before being linked.

## 9. Engineering principles

`DEVELOPMENT_PRINCIPLES.md` defines the long-term engineering standards for Reforged. Read it when making architecture, runtime, or code quality decisions.

## 10. Current project position after CodeUpdate-0049

- Current Code Update: `0049`
- Current Database Version: `0009`
- Current Milestone: `Milestone 2 - Engine Modernization / Host & World Editor Planning`
- Milestone 1: frozen
- Current priority: push CodeUpdate-0049, verify GitHub Actions green by user report, then continue DataDecode0069 Spell Executable Dataflow Proof using the GitHub repository as the direct working copy.

CodeUpdate-0031A is a corrective build-fix patch for CodeUpdate-0031. It fixes invalid C# string escaping in `Tests/Source/Reforged.Core.Tests/Program.cs`. The user reported CodeUpdate-0031A GitHub Actions green, and Codex local .NET 8.0.422 sequential restore/build/smoke verification passed. It does not add gameplay features, networking, combat, character creation, or world simulation.

CodeUpdate-0032 improves non-gameplay Server bootstrap diagnostics by capturing ProjectManifest identity fields. The user reported CodeUpdate-0032 GitHub Actions green.

CodeUpdate-0033 makes Server bootstrap exit-code behavior an explicit report contract. Future updates should continue recording completed work, boundaries, verification status, and next-scope candidates separately.

CodeUpdate-0034 records OmniVerse as a read-only reference source for future data audits. OmniVerse data may identify possible mismatches, but it must not automatically overwrite Reforged `Data/` or `ImportedDatabases/`; any correction requires a reviewed and approved follow-up update.

CodeUpdate-0035 adds Server bootstrap validation issue counts for startup output and future host tooling. It does not add gameplay, editor behavior, networking expansion, or data rewrites.

CodeUpdate-0036 adds knowledge-base, design-decision, reference-source, and discussion-recap documentation through direct GitHub commits.

CodeUpdate-0037 combines the user-provided CodeUpdate-0036B continuity cleanup package and CodeUpdate-0037 Software Design Specification package into one documentation-only local update. It adds draft `Docs/SDS/` planning records and repairs stale continuity. It does not add gameplay, editor behavior, networking expansion, source-code behavior changes, or data rewrites.

CodeUpdate-0038 adds required-directory status reporting to the non-gameplay Server bootstrap report and startup output. It records CodeUpdate-0037 as green by user report and does not add gameplay, editor behavior, networking expansion, or data rewrites.

CodeUpdate-0039 adds a compact host-tool readiness summary to the non-gameplay Server bootstrap report and startup output. It records CodeUpdate-0038 as green by user report and keeps gameplay status explicitly disabled.

CodeUpdate-0040 preserves WCC-labeled WBB material under `ImportedDatabases/WCCLabeledWBB/` as reference/import material only.

CodeUpdate-0041 adds the read-only Classic parity audit foundation to Server bootstrap. It inventories canonical `Data/` and imported reference material but does not parse proprietary legacy formats and does not claim exact MajorMUD parity.

CodeUpdate-0042 adds read-only canonical Server data catalog loading to bootstrap. It loads registered Core data categories from `Data/`, reports category status and record counts, and keeps gameplay disabled.

CodeUpdate-0043 classifies canonical Server data categories as runtime-ready or runtime-blocked. It reports empty placeholders without failing startup or starting gameplay.

CodeUpdate-0044 adds the original reference capture/extractor tool under `Tools/ReferenceCapture/`. It preserves and inventories original/reference material, extracts ZIP files directly, can extract RAR/7z files when 7-Zip is available, captures printable strings, and writes evidence reports. It does not run original executables, does not use keygens, does not rewrite canonical `Data/`, does not delete imported/reference data, does not add gameplay, and does not claim exact MajorMUD parity.

CodeUpdate-0045 integrates the user-provided DataDecode0043 Import Readiness Validation checkpoint under `DataDecodeCheckpoints/DataDecode0043/`. DataDecode0043 estimates static binary decode at approximately 96-97%, registers 5 import blockers, reviews 1,000 foreign-key candidates, hashes 83 native source files, and promotes 0 canonical fields. It is a pre-mirror baseline only and does not change server behavior or canonical `Data/`.

CodeUpdate-0046 imports the gathered OmniVerse/MudInfo reference payload under `ImportedDatabases/OmniVerseReference/CodeUpdate0046/` for validation/correlation only. It includes WCC OmniVerse API captures for abilities, classes, items, monsters, races, shops, spells, world status, 168 shop-seeded roommap files, 1,246 room detail files, a 23-entry room capture error log, the MudInfo `maps.rar` source archive, and 102 extracted MudInfo map files. HTTrack captured the OmniVerse SPA shell/assets externally under `E:\Httracks\OmniVerseCodexFull`, but the committed verification payload is the API/map evidence, not the web shell cache. Original MajorMUD binary files remain authoritative; CodeUpdate-0046 does not rewrite canonical `Data/`, promote fields, add server behavior, add gameplay, or assume OmniVerse public IDs equal binary row indexes.

CodeUpdate-0047 preserves the user-provided DataDecode0049 WGSERVER record access / structure correlation checkpoint under `DataDecodeCheckpoints/DataDecode0049/` and records the DataDecode0050 GitHub transition handoff under `DataDecodeCheckpoints/DataDecode0050/`. DataDecode0049 reports 15 NativeRuntime DAT datasets profiled, 5,916 executable strings, 9,574 record constant hits, 29 string-address xrefs, 29 record-access candidate windows, 15 descriptor candidates, 93 canonical term hits, and 0 field promotions. DataDecode0050 changes the decode workflow: future decode passes must update the GitHub repository directly, with ZIPs allowed only as archival/checkpoint artifacts. No standalone DataDecode0050 ZIP was supplied in this Codex turn; dispatcher/message items are handoff continuity only until supported by committed reports or binary-backed evidence.

CodeUpdate-0048 creates `DataDecodeCheckpoints/UnifiedDecodedData/` as a repo-native index and comparison surface for current decoded data and the CodeUpdate0046 OmniVerse/MudInfo reference payload. It adds generated inventory, category comparison reports, and an Android ChatGPT decode workflow under `Docs/DATDecoding/AndroidChatGPTDecodeWorkflow.md` plus phone handoff templates. It does not move historical reports, rewrite canonical `Data/`, promote fields, add server behavior, or add gameplay.

CodeUpdate-0049 preserves the user-provided DataDecode0068 Spell Engine Proof checkpoint under `DataDecodeCheckpoints/DataDecode0068/` and refreshes `DataDecodeCheckpoints/UnifiedDecodedData/`. DataDecode0068 reports 1,180 canonical spell records, 930 native WCCSPELS records, 1,789 native WCCSPELS string rows, 1,350 OmniVerse/WCC spell reference records, 1,151 canonical `sourceId` to OmniVerse `legacy_number` matches, 41 canonical spell raw field positions, 10 reference MHT files scanned, 505 repo files with spell-term hits, 5,916 WGSERVER printable strings, 46 WGSERVER spell-term string rows, 439 WGSERVER spell constant probe rows, 8 evidence matrix rows, and 0 field promotions. DataDecode0051 through DataDecode0067 are not claimed as repo-preserved unless supplied later.

Server direction after CodeUpdate-0039: first real Server target is Classic baseline only. Keep architecture mode-ready, but do not implement Reforged mode, Custom mode, AI systems, alternate gameplay rules, or mode/profile loading until explicitly approved. `Data/` defines world/content; Server defines and enforces engine behavior; Editor should eventually edit data safely.

Engine direction after CodeUpdate-0039: favor reusable engine systems over feature-specific systems. Avoid designing around Classic unless behavior is truly Classic-specific. Prefer data-driven world behavior, reusable engine behavior, and editor-visible structures where practical. The WCC default install under `ImportedDatabases/WCCDefaultInstall/` is reference/import material only.

All WCC data is reference-only unless the user explicitly approves a reviewed import/reconciliation update.

Imported/reference data may eventually be retired only after Reforged-native `Data/` is complete, verified, and explicitly approved for cleanup.

Future extraction work should remain staged as capture, decode, compare, and reviewed apply steps. Capture and decode outputs are evidence only until a separate approved reconciliation update changes canonical `Data/`.

Original MajorMUD binary files remain authoritative. Imported data, MegaMud files, OmniVerse snapshots, mudinfo references, maps, WBB comparisons, website data, community data, and tool-derived data are validation/correlation evidence only. Never assume public OmniVerse IDs equal binary row indexes. Never promote noisy raw U16 matches or candidate fields without corroborating binary evidence.

DataDecode workflow after CodeUpdate-0047: stop using checkpoint ZIPs as independent working repositories. Every future decode pass starts from the GitHub repository, commits reports/metrics/handoffs directly, preserves historical reports, and updates continuity documents. ZIPs may be exported for archival use, but GitHub remains the master copy.
