# MajorMUD Reforged — Bundle D Revision 0006

## DONE

Phase D6 Cleanup completed end-to-end.

- Established `RuntimeClassicCleanupService.CleanupRoomGround` as the named authoritative room-ground cleanup consumer.
- Restricted cleanup execution to `RuntimeItemLocations.Room`; character, equipment, NPC, container, corpse, merchant, and other ownership domains are untouched.
- Preserved verified cleanup behavior: ordinary room-ground items become hidden, room currency becomes hidden, and decoded `Del@Maint` (ability 119; native display text: `Delete at cleanup`) items are deleted.
- Made cleanup replay explicitly idempotent by retaining and separately reporting already-hidden item state; room currency already had equivalent idempotence.
- Removed the generic `cleanup_exempt` metadata bypass from the authoritative path.
- Retained gang-house, vault, and gang-house-shop protection only through matching deed ownership and current upkeep.
- Connected cleanup-hidden item state to the existing Search, pickup, visible re-drop, item-state persistence, and currency-state consumers.
- Added executable `--audit-bundle-d-cleanup` validation and cumulative installer execution.

## LEFT

Phases D7 through D11: Hidden Object Rules, Teleporters, Environment, Traps, and Maps. Bundle H retains final cross-bundle runtime consolidation.

## VERIFIED

- Room-ground items and currency become hidden during cleanup.
- Items decoded with `Del@Maint` (ability 119; native display text: `Delete at cleanup`) are deleted.
- Cleanup-hidden objects stay hidden until successfully searched for pickup.
- Pickup followed by re-drop creates a visible room object.
- Gang-house retention requires a matching active deed and paid upkeep.
- Character inventory and equipment are outside room-ground cleanup ownership.

## STRONGLY CORROBORATED

- Replaying cleanup preserves existing hidden state and does not create a second transition.
- Item metadata and room-currency state are the authoritative persisted cleanup representations.

## DECODED BUT NOT MAPPED

None introduced by this pass.

## CONFLICTS

The prior generic `cleanup_exempt` metadata bypass conflicted with deed-and-upkeep-qualified gang-house retention and was removed from the authoritative cleanup path.

## TRUE UNKNOWN

- No verified universal Classic cleanup interval or wall-clock schedule was found; no interval was invented.
- Native ordering when world reset and cleanup become due simultaneously remains unresolved.

## Files Added

- `Apps/Source/Reforged.Server/RuntimeBundleDCleanupAudit.cs`
- `Librarium/Reports/bundle_d_revision_0006.md`

## Files Modified

- `Apps/Source/Reforged.Server/RuntimeClassicCleanup.cs`
- `Apps/Source/Reforged.Server/Program.cs`
- `INSTALL_AND_VALIDATE.bat`

## Files Removed

None.

## Duplicate Paths Removed

The generic metadata exemption execution path was removed. No source files were removed.

## Build Result

Not executed locally because the available environment has no .NET SDK. Installer requires a warnings-as-errors win-x64 build.

## Audit Result

Dedicated cleanup audit added and wired into the cumulative installer. Static package checks and ZIP integrity passed locally.

## Publish Result

Performed by `INSTALL_AND_VALIDATE.bat` to `Release\MajorMudReforged`; it verifies `Reforged.Server.exe` before success.
