# Bundle H Baseline Runtime Consolidation Audit

Date: 2026-07-23

Scope: CodeUpdate0139 dirty local working copy after Decode Exhaustion Bundles F and G. This report captures the Bundle H Revision 1 baseline and the first safe audit repairs. It does not claim GitHub Actions status.

## Baseline Build

- Command: `dotnet build Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-restore -warnaserror`
- Result before edits: PASS, 0 warnings, 0 errors.
- Result after first safe repairs: PASS, 0 warnings, 0 errors.
- Result after Bundle A/D9 evidence-bounded repairs: PASS, 0 warnings, 0 errors.

## Full Audit Sweep

The monolithic audit sweep executed 60 audit commands.

- Passing before fixes: 53/60.
- Failing or timed out before fixes:
  - `--audit-character-bundle` exit 32.
  - `--audit-bundle-d-doors` exit 87.
  - `--audit-bundle-d-search` exit 88.
  - `--audit-bundle-d-environment` exit 93.
  - `--audit-bundle-d-corrective` exit 90.
  - `--audit-bundle-d-final` exit 96.
  - `--audit-bundle-e-zero-disconnects` exit -1 after manual timeout.

## Applied Safe Repairs

- Added `search` to `RuntimeCommand.SupportedCommands`; dispatch already routed `search` to `ExecuteClassicSearch`.
- Updated Bundle D search audit expectations to match the accepted Classic behavior: every SEARCH rerolls eligible room targets and replaces the prior transient result set.
- Scoped Bundle D door audit validation to door/world-object failures so unrelated living-world validation categories do not fail the door gate.

## Verification After Repairs

- `dotnet build Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-restore -warnaserror`: PASS.
- `--audit-bundle-d-search`: PASS.
- `--audit-bundle-d-doors`: PASS.
- `--audit-bundle-d-corrective`: PASS.
- `--audit-bundle-d-final`: FAIL only on D9 Environment.

## Follow-Up Verification After Bundle H Repairs

### Bundle A Character Audit

`--audit-character-bundle` now passes.

- Race count: 13.
- Class count: 15.
- True generated-unconsumed Bundle A gameplay fields: 0.
- Repository-wide evidence-name-only warnings recorded separately: 57.

### Bundle D Environment Audit

`--audit-bundle-d-environment` now passes with complete decoded environment coverage:

- Every decoded environment row resolves to a runtime room.
- Every room with a decoded environment row consumes environment provenance.
- Every runtime room has a map-qualified environment record.
- `room.map1.288` / `The Silver River` and `room.map1.2306` / `Small House, Attic` are backed by imported WCC room records and source-classified as `decoded_imported_wcc_room_record_anchor`.
- D9 explicitly asserts both imported WCC anchor records and their decoded values:
  - `room.map1.288`: `room_type=0`, `light_level=0`, `gang_house=0`, `attributes=0`, `death_room_id=0`, `respawn_minutes=5`, `max_area_cap=0`, `linked_spawn_room=2342`, `room_lair_id=21`.
  - `room.map1.2306`: `room_type=1`, `light_level=0`, `gang_house=0`, `attributes=0`, `death_room_id=0`, `respawn_minutes=5`, `max_area_cap=0`, `linked_spawn_room=0`, `room_lair_id=10`.
- Gang-house environment consumption must match the decoded catalog; the decoded catalog currently contains zero gang-house rows.

Verified counts:

- Runtime rooms: 26,668.
- Environment records: 26,668.
- Gang-house environment rows: 0.

No gang-house behavior was invented for these rows.

### Bundle D Final

`--audit-bundle-d-final` now passes.

- `AuditPassed`: true.
- `BundleDComplete`: true.
- D1 through D12 phases: PASS.

### Bundle E Zero Disconnects

The timeout-safe rerun completed successfully.

- `--audit-bundle-e-companion-lifecycle`: PASS.
- `--audit-bundle-e-merchant-trainer`: PASS.
- `--audit-bundle-e-boss-death-respawn`: PASS.
- `--audit-bundle-e-npc-item-control-persistence`: PASS.
- `--audit-bundle-e-relationship-service`: PASS.
- `--audit-bundle-e-zero-disconnects`: PASS.
- `BundleEComplete`: true.
- NPC definitions: 1,100.
- Spawn records: 962.
- NPC/creature subsystems passed: 13/13.
- Duplicate authoritative creature source paths: 0.

The first-sweep timeout is now closed as a local verification blocker.

## Arena Probe Note

Bundle H confirmed the canonical Newhaven Arena validation target is `room.map1.2150`. Older reports still mention stale probes such as `room.map1.203`, `room.map1.488`, or exact display-name lookup. Active Bundle D room validation uses `room.map1.2150`.

## Current Bundle H Gate

Date: 2026-07-23

Raw logs and summary are preserved under `Reports/BundleH/CurrentGate/`.

- `dotnet build Apps/Compiler/MajorMud.Reforged.Compiler.csproj --configuration Release --no-restore -warnaserror`: PASS.
- `dotnet build Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-restore -warnaserror`: PASS.
- `--audit-character-bundle`: PASS.
- `--audit-bundle-d-search`: PASS.
- `--audit-bundle-d-doors`: PASS.
- `--audit-bundle-d-corrective`: PASS.
- `--audit-bundle-d-environment`: PASS.
- `--audit-bundle-d-final`: PASS.
- `--audit-bundle-e-zero-disconnects`: PASS.

Bundle H current gate result: PASS for the seven focused runtime audits; the added gang-house service-location audit also passes and is reported separately below.

The old D9 limitation about missing generated room environment records is closed locally. `room.map1.288` and `room.map1.2306` are verified imported WCC room-anchor environment records, D9 requires them explicitly, and the stale corrective/final audit note that room environment fields do not reach generated Classic records has been removed.

## Gang-House Source Audit

Date: 2026-07-23

- Command: `dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --audit-bundle-h-gang-houses`
- Result: PASS with `RuntimeGameplayReady=true` for generated WCC shop API service locations.
- Raw log: `Reports/BundleH/CurrentGate/audit-bundle-h-gang-houses.log`.
- JSON report: `Reports/BundleH/CurrentGate/bundle_h_gang_house_audit.json`.

Verified:

- `ImportedDatabases/NativeRuntime/WCCGANGS.DAT` exists and is 2,048 bytes.
- `ImportedDatabases/WCCDefaultInstall/wcc default/wccgang2.dat` exists and is 28,672 bytes.
- WCCGANGS decode summary, block samples, and string catalog are present.
- All ten MudInfo gang-house reference maps are present.
- Generated Classic data contains ten parchment deed items.
- Generated Classic data contains 22 gang-house/deed shop records.
- Librarium contains `Librarium/Objects/gang_houses/gang_house_records.json`.
- Generated Classic data contains `Data/Classic/gang_houses.json`.
- Runtime loads the generated catalog through `RuntimeClassicGangHouseCatalog`.
- Generated service-location scope is 10 Classic houses, 20 WCC shop API-backed service locations, 10 vault rooms, and 10 supply-shop rooms.
- All promoted service locations resolve to runtime rooms.
- `RuntimeClassicGangHouseProtectionService` requires matching deed ownership and current upkeep before protecting cleanup.
- Runtime protection checks pass for current deed ownership, expired deed rejection, and non-gang-room rejection.

Remaining limits:

- Generated `Data/Classic/environment_rooms.json` currently has 0 native gang-house flags; active service locations are supplied by `Data/Classic/gang_houses.json`.
- Only WCC shop API service rooms are promoted in this pass.
- Ordinary interior house room ownership coverage remains blocked until exact map-qualified ownership/location evidence is recovered.
- Persistent deed acquisition/upkeep mutation is still not a complete live gameplay flow; this pass wires cleanup protection consumption of the generated catalog.
- The listed `LibrariumLocal/core_graph.json` and `LibrariumLocal/runtime_index.json` handoff files are not present in this repository.
- MudInfo maps remain reference evidence for layout/legend only; no room IDs were promoted from map art.
