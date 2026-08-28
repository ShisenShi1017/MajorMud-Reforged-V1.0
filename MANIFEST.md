# Manifest

## CodeUpdate0140

Scope:

- Repository cleanup and imported/reference evidence archival after the CodeUpdate0139 runtime baseline.
- Loose `ImportedDatabases/` and `Evidence/` content retired from Git, with local-only archive preservation.
- Librarium remains committed gameplay authority; generated `Data/Classic` remains untracked runtime output.
- Runtime saves use realm-scoped roots: `Saves/Classic`, `Saves/Custom/<realm>`, and `Saves/Host`.
- Character creation allows up to three characters per login per world.
- Active continuity/manifests updated to identify CodeUpdate0140 as the current push baseline.
- PASS 43-49 parity continuations, culminating in update 0320 native Classic death-experience correction and exact ordinary-death execution.

Verification:

- `dotnet build MajorMud.Reforged.sln --configuration Release --no-restore`: PASS locally with 0 warnings and 0 errors.
- Compiler regression: PASS at 104,911 records across 47 families.
- Classic generation: PASS at 103,330 objects across 36 families and 41 files, including 26,668/26,668 room descriptions.
- Focused PASS31, PASS39, and PASS43-49 runtime smokes: PASS.
- Core/server smoke: PASS 17/17.
- Python PASS49 validator: NOT RUN because no interpreter is registered locally.
- `python Tools\validator.py`: PASS locally with 15/15 current-health checks.
- GitHub Actions: pending until pushed and confirmed green.
- PASS 48 cumulative verification: full solution Release build PASS with 0 warnings/errors; compiler regression PASS at 104,911 records / 47 families; Classic generation PASS at 103,330 objects / 36 families / 41 files; focused PASS31 and PASS43-48 smokes PASS; core/server smoke PASS 17/17.
- PASS 48 Python validator: not run because the Windows Python launcher reports no installed interpreter.

Known limitation:

- CodeUpdate0140 does not add gameplay systems; it is a cleanup/archive baseline.

## CodeUpdate0139

Scope:

- Telnet runtime cohesion.
- Classic runtime audit tooling.
- Librarium room-description promotion from available native WCC map DAT evidence.
- OmniVerse map-qualified room-description reference recovery for remaining canonical gaps.
- WCCMP002 default-install binary room-description recovery for repeated/duplicate room templates.
- WCCMP002 placeholder room-description recovery for the final placeholder-named room gaps.
- WCC Librarium exhaustion audit.
- MMExplorer SQL/MDB and OmniVerse public codex Librarium reconciliation.
- Runtime command abbreviation centralization.
- Map-qualified live spawn placement repair and room-scoped entity rendering.
- Room native/passive effect and WCCMSG room message decode closure.
- Generated NPC quest dialogue dispatch from decoded textblock stage scripts.
- Generated quest/script edge closure for random tables, world-intent target validation, host teleport/summon commits, evidence-gated remoteaction event recording, room/object event recording, repeatability guard behavior, and exhaustive NPC quest-entry dispatch validation.
- Final native closure/WCC decode archive intake as evidence and Librarium/reference material.
- Verified/native and strongly-correlated Librarium sidecar activation into generated runtime data.
- Reforged app Librarium browser tab with searchable category views, name-only lists, curated game-facing stat details, and readable parchment text.
- Reforged app Client play flow with server/session launcher, dedicated black terminal window, menu tools, command input, and separate tabbed options window.

Key changed areas:

- `Apps/Source/Reforged.Server/`
- `Apps/Compiler/`
- `Librarium/Objects/room_descriptions/`
- `Librarium/Objects/abilities/`
- `Librarium/Objects/items/`
- `Librarium/Objects/spells/`
- `Librarium/Objects/npcs/`
- `Librarium/Objects/shops/`
- `Librarium/Objects/character_foundation/`
- `Librarium/Objects/scripts/`
- `Librarium/Objects/triggers/`
- `Librarium/Policies/`
- `Tools/Importer/`
- `Reports/RuntimeVerification/`
- `Reports/DataDecode/`
- Root continuity docs and manifests
- `Reference/FinalNativeClosure/`
- `Reports/PackageIntake/`
- `Apps/Source/Reforged.App/Views/LibrariumView.xaml`
- `Apps/Source/Reforged.App/Views/LibrariumView.xaml.cs`
- `Apps/Source/Reforged.App/Views/PlayView.xaml`
- `Apps/Source/Reforged.App/Views/PlayView.xaml.cs`
- `Apps/Source/Reforged.App/Views/ClientTerminalWindow.xaml`
- `Apps/Source/Reforged.App/Views/ClientTerminalWindow.xaml.cs`
- `Apps/Source/Reforged.App/Views/ClientOptionsWindow.xaml`
- `Apps/Source/Reforged.App/Views/ClientOptionsWindow.xaml.cs`
- `Apps/Source/Reforged.App/Resources/Theme.xaml`

Verification:

- Server Release build: PASS locally.
- Test Release build: PASS locally.
- Core/server smoke harness: PASS locally.
- Compiler room-text audit: PASS locally.
- Room-description reference recovery: PASS locally; 800 gap-fill records promoted.
- WCCMP002 placeholder recovery: PASS locally; 140 additional WCCMP002 1.11p room/map records promoted and WBBMP002-corroborated.
- Compiler smoke output: PASS locally; 26,668/26,668 room descriptions emitted during current Classic generation checks.
- WCC Librarium exhaustion audit: PASS locally.
- Server Classic data audit: PASS locally; 26,668 rooms with descriptions and 0 without descriptions.
- Complete runtime verification: PASS 69/69 at `Reports/RuntimeVerification/validator_latest.md`.
- Quest dialogue dispatch smoke: PASS locally; 182 decoded stage scripts, 83 NPC quest-entry triggers, 182 linked script refs, item consume/reward transition, and failure/no-mutation behavior.
- Quest/script edge closure: PASS locally; random table execution, cast/summon/remoteaction coverage, remoteaction event recording, room/object event recording, world-intent target validation, repeatability guard coverage, and exhaustive generated NPC quest-entry dispatch validation.
- Final native closure archive intake: PASS locally; 1,673 files copied, user-confirmed server source slices merged into active runtime code, server/test builds PASS, and validator PASS.
- Verified/strong activation: PASS locally; `room_interactions` compiles three source files including strongly-correlated sidecars, generated `triggers` contains 367 strong records, and unresolved exit targets are compiler-blocked.
- Reforged app Librarium browser: PASS locally; app Release build succeeds, app publish succeeds to `Release/MajorMudReforged/MajorMudReforged.exe`, and parchment text is dark/readable.
- Reforged app Client terminal-window UX: PASS locally; app Release build/publish succeeds, and startup smoke confirms the published EXE remains running after launch.
- Current validator: PASS 13/13 locally with `server_interaction_narrative` included.
- Living-world placement smoke: PASS locally.
- Focused `room.map1.21` audit: PASS locally; one neutral room-scoped NPC and no group-candidate leakage.
- GitHub Actions: pending until pushed and confirmed green.

Known limitation:

- WBBMP002 is secondary comparison evidence only for the WCCMP002 placeholder recovery.
- Current generated evidence for `room.map1.21` resolves to `guardsman` (`npc.14`), not the user-expected `elite guardsman`.
- Two unresolved spawn placements are logged and skipped: `spawn.288.43` and `spawn.2306.213`.
- `remoteaction` is validated and recorded as a host event; exact native remote mutation remains evidence-gated.
- User-confirmed final native closure implementation slices are active runtime code after merge/replace cleanup; do not recreate duplicate reference copies.

## CodeUpdate0138

Scope:

- Server runtime and host enablement.
- Generated Classic `/Data` refresh from Librarium.
- Runtime smoke and complete verification gate updates.
- Librarium movement promotion from MMExplorer evidence.
- Librarium textblock quest-flow fix 0142.
- Compact active documentation update.

Key changed areas:

- `Apps/Source/Reforged.Server/`
- `Apps/Compiler/`
- `Apps/Compiler.Tests/`
- `Config/`
- `Data/Classic/`
- `Librarium/`
- `Runtime/`
- `Sim/`
- `Tests/`
- `Tools/Validation/`
- `Tools/MDBExtractor/`
- `Reports/RuntimeVerification/`
- Root continuity docs and manifests

Verification:

- `python Tools\validator.py`
- PASS 68/68

One-time validation proofs are recorded in ValidationManifest.json.

## GuardianHallPhase3-0150

- Realm Keeper local runtime telemetry service and observable dashboard metrics.
- Initial/manual/30-second refresh.
- Generated Data, Saves, validator/build report, runtime-log, and crash evidence sources.
- Static validator: `Tools/GuardianHall/validate_guardian_hall_phase3.py`.

## Guardian Hall Phase 3-0151
See `GuardianHallPhase3-0151_MANIFEST.json` for the changed-file hashes.

## Guardian Hall Phase 3-0152

- `Apps/Source/Reforged.App/Guardians/GuardianModels.cs`
- `Apps/Source/Reforged.App/Guardians/GuardianServices.cs`
- `Apps/Source/Reforged.App/ViewModels/GuardianHallViewModel.cs`
- `Apps/Source/Reforged.App/Views/GuardianHallView.xaml`
- `Tools/GuardianHall/validate_guardian_hall_phase3_0152.py`
- `Docs/GuardianHall/PHASE3_ANALYST_RUNTIME_0152.md`
- `Reports_GuardianHallPhase3_0152_validation.txt`
- `GuardianHallPhase3-0152_VALIDATION_SUMMARY.txt`
- `Release/MajorMudReforged/BUILD_REPORT_GUARDIAN_HALL_PHASE3_0152.txt`
- continuity documentation and package manifests

## Guardian Hall Phase 3-0155

Cumulative Guardian Hall update through Archivist report/export completion and editable local-tool validation/launch. The update-only package installs from its own top-level folder into the main repository, runs the Phase 3-0155 validator, restores/builds the solution, and publishes `Release\MajorMudReforged\MajorMudReforged.exe` when .NET 8 is available.

## Guardian Hall Phase 3-0156

Cumulative Guardian Hall update through Worldsmith approval-controlled test-realm staging. Includes hash-bound approval artifacts, separate confirmation-code entry, safe-target enforcement, isolated apply output, rollback backups, JSON receipts, updated validators, documentation, and the top-folder install-and-validate package.

## GuardianHallPhase3-0157
Adds schema-versioned, one-time migration from Phase 1 `assignments.json` into dedicated Guardian workspaces. See `GuardianHallPhase3-0157_MANIFEST.json`.

## Guardian Hall Phase 3-0159
- Remote Realm Keeper HTTP telemetry endpoint registry
- Environment-variable bearer authentication without persisted secrets
- Local and remote telemetry aggregation
- Endpoint status and failure visibility

## Guardian Hall Phase 3-0161
Includes Analyst trend persistence, chart-series UI, report package exports, validator, documentation, and cumulative packaging metadata.

## Guardian Hall Phase 3-0162
Includes Archivist indexed evidence search, selectable relationship browsing, reverse cross-reference display, validator, documentation, and cumulative packaging metadata.

## Guardian Hall Phase 3-0164

- Atomic Guardian Hall state persistence with temporary-file replacement and recoverable backup.
- Schema version 7 diagnostics persistence.
- In-app Guardian Hall self-test with five-role, role-safety, connector-bound, scheduler, preview-only, and save/reload checks.
- Validator: `Tools/GuardianHall/validate_guardian_hall_phase3_0164.py`.
- Windows Release build/publish remains delegated to the combined update installer when .NET 8 is available.
