# CodeUpdate0139 Room Text Recovery Staging

## Scope

This report records the CodeUpdate0139 room-description recovery audit, Librarium promotion, and staging verification pass.

The live `Data/Classic` directory was not regenerated or published during this pass.

## Source Trace

Known room: `room.map1.2141` / `Newhaven, Weapons Shop`.

- Source room record: `Librarium/SourceImports/MMExplorerSQLData/DecodedRooms0138/rooms_decoded.json`.
- Librarium runtime record: `Librarium/Objects/rooms/mmexplorer_room_records_0140.json`.
- Native description evidence: `ImportedDatabases/NativeRuntime/WCCMP001.DAT`.
- Librarium promoted descriptions: `Librarium/Objects/room_descriptions/native_map_room_description_records_0139.json`.
- Compiler staging output: `Data/CompilerSmokeTest/ClassicRoomText0139/rooms.json`.
- Live runtime output: `Data/Classic/rooms.json`.

The source room record and Librarium runtime record contain identity, exits, NPC/shop evidence, and fixed raw fields, but no inline description field. The native map DAT contains the room title and description text in a 1536-byte room block.

## Audit Result

`dotnet run --project Apps/Compiler/MajorMud.Reforged.Compiler.csproj --configuration Release --no-build -- audit-room-text`

- Source rooms: 26,666.
- Inline descriptions: 0.
- Description ref IDs: 2,417.
- Long description ref IDs: 0.
- Native text records loaded: 2,417 Librarium room-description records.
- References resolved: 2,417.
- References unresolved: 21,804.
- Emitted non-empty descriptions in staging: 2,417.
- Missing promotable native map files: `WCCMP002.DAT` through `WCCMP017.DAT`. The WCC exhaustion audit records local `wccmp002.dat` as available reference evidence, but it is not promoted because the current parser produced mixed text fragments; `WCCMP003.DAT` through `WCCMP017.DAT` are not locally available.

## Required Probe Result

Librarium-promoted records and staging output contain non-empty binary-backed descriptions for:

- `room.map1.2140` / `Newhaven, Village Entrance`.
- `room.map1.2141` / `Newhaven, Weapons Shop`.
- `room.map1.203` / `Arena Entrance`.
- `room.map1.348` / `Adventurer's Guild, Foyer`.
- `room.map1.488` / `Arena`.

Live `Data/Classic` still contains 0 room descriptions because publish was not approved.

## Command Parser Result

`dotnet run --project Apps/Source/Reforged.Server/Reforged.Server.csproj --configuration Release --no-build -- --smoke-live-gameplay`

Passed parser checks for:

- `l`, `lo`, `look`.
- `i`, `inv`, `inventory`.
- `eq`, `equipment`.
- `stat`, `stats`, `status`, `score`.
- `n`, `north`.
- `op n`, `ba north`.
- Ambiguity rejection for `s`, `se`, and `st`.

## Limitation

The full-world room-description gate cannot truthfully pass until the remaining native map DAT files are available or an equivalent binary-backed source is approved.

Related source-coverage report: `Reports/DataDecode/wcc_librarium_exhaustion_audit_0139.md`.
