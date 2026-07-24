# MajorMUD Reforged — Bundle D Revision 0002

## DONE
Phase D2 Rooms completed end-to-end.

- Preserved all generated room authority fields in `RuntimeRoom` instead of discarding them after JSON loading.
- Canonical map-qualified IDs remain the sole room identity authority.
- Added live runtime consumers for authoritative room metadata and reset placed-item IDs.
- Preserved room names, descriptions, NPC definitions, command definitions, world scope, relationships, evidence counts, Classic lock, mapping provenance, generator provenance, and validation provenance.
- Preserved imported room type, shop number, and light level values without inventing semantics.
- Preserved fixed placed-item slot semantics and enforced the decoded fallback: pending item values spawn nothing.
- Added an executable Bundle D room audit.
- Validates Newhaven Arena only as `room.map1.2150`.
- Reviewed authoritative room loader paths; no parallel room loader was added.

## LEFT
Phase D3 Exits is next.

## VERIFIED
- Every generated room loads exactly once.
- Canonical room IDs and `(map, room)` keys are unique.
- Live room lookup, rendering, NPC population, and metadata access use `RuntimeWorld`/`RuntimeRoom`.
- Newhaven Arena is `room.map1.2150`.
- Room evidence and Classic-lock metadata reach runtime.

## STRONGLY CORROBORATED
- Ten fixed placed-item slots are represented by `RuntimeRoomPlacedDefinition`.
- Unrecovered placed-item values produce no spawn rather than invented IDs.
- Imported room type, shop number, and light level are retained for later consumers.

## DECODED BUT NOT MAPPED
- `command_definition` identifiers are preserved, but command execution semantics remain deferred to the integrated command/trigger/script mapping.
- Terrain, region, coordinates, ownership, general flags, and special properties are absent from the current generated Classic room schema.

## CONFLICTS
No duplicate canonical room identity is accepted. Duplicate map-qualified identities fail loading.

## TRUE UNKNOWN
- Concrete placed-item IDs where MDB long-value recovery remains pending.
- Exact gameplay meaning of imported numeric room-type values.

## Files Added
- `Apps/Source/Reforged.Server/RuntimeBundleDRoomAudit.cs`
- `Librarium/Reports/bundle_d_revision_0002.md`

## Files Modified
- `Apps/Source/Reforged.Server/Program.cs`
- `Apps/Source/Reforged.Server/RuntimeWorldModels.cs`
- `Apps/Source/Reforged.Server/RuntimeWorldLoader.cs`
- `INSTALL_AND_VALIDATE.bat`

## Files Removed
None.

## Duplicate Paths Removed
None. No exact duplicate room execution path was proven.

## Build Result
Installer enforces Release win-x64 restore/build with warnings as errors. Local container has no .NET SDK, so no local compilation result is claimed.

## Audit Result
Installer runs prior audits plus `--audit-bundle-d-world-authority` and `--audit-bundle-d-rooms`.

## Publish Result
Installer publishes to `Release\MajorMudReforged` and verifies `Reforged.Server.exe`.
