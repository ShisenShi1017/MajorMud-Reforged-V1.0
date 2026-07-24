# Bundle C Revision 0011 — C10 Runtime Validation and Search Correction

## DONE

- Corrected Classic hidden-object SEARCH to reroll every eligible hidden object on every command.
- Removed persisted per-player hidden-discovery state.
- Replaced accumulated discovery with a transient latest-search result set; a new SEARCH can reveal a different subset in a crowded hidden room.
- Preserved hidden ground state until pickup and visible redrop behavior.
- Added the Bundle C completion audit, which executes and requires all C1–C9 subsystem audits together.
- Added explicit runtime-owner, consumer, persistence-owner, evidence, conflict, and true-unknown closure records for every Bundle C subsystem.

## LEFT

- No Verified or Strongly Corroborated Bundle C equipment, item, inventory, merchant, economy, loot, or persistence field remains decoded-only when the completion audit passes.
- Bundle H retains final runtime consolidation and native-only cleanup.

## VERIFIED

- SEARCH results are recalculated every time SEARCH is used.
- Multiple hidden objects can produce different search results on consecutive searches.
- Search results are transient and are not restored after restart.
- Hidden objects remain hidden on the room ground until pickup.
- Picking up and dropping a formerly hidden object makes the dropped object visible.
- Copper, silver, gold, platinum, and runic remain physical, denomination-preserving currency.

## STRONGLY CORROBORATED

- Effective Perception versus concealment remains the active search comparison, while exact native primary-stat derivation stays external.
- Atomic save generation, integrity backup, and reconciliation remain the authoritative persistence scheme.

## DECODED BUT NOT MAPPED

- None at Verified or Strongly Corroborated evidence level after all Bundle C audits pass.

## CONFLICTS

- Revision 0010 persisted per-player hidden discoveries. This conflicted with supplied Classic behavior and is removed by Revision 0011.

## TRUE UNKNOWN

- Exact native weapon-type/armor-type compatibility relationship matrices.
- Exact native carry-capacity and encumbrance thresholds and penalties.
- Universal temporary-item decay interval.
- Native-only item effects without a verified decoded consumer.

## Files Added

- `Apps/Source/Reforged.Server/RuntimeBundleCCompletionAudit.cs`
- `Librarium/Reports/bundle_c_revision_0011.md`

## Files Modified

- `Apps/Source/Reforged.Server/RuntimeClassicHiddenSearch.cs`
- `Apps/Source/Reforged.Server/RuntimeBundleCPersistenceAudit.cs`
- `Apps/Source/Reforged.Server/Program.cs`
- `INSTALL_AND_VALIDATE.bat`

## Files Removed

- None.

## Duplicate Paths Removed

- Persisted hidden-discovery file path.
- Accumulating previously-discovered search bypass.

## Build Result

- Installer requires `dotnet build -warnaserror` with 0 warnings and 0 errors.
- Local package environment does not contain the .NET SDK.

## Audit Result

- Static source/package checks passed.
- Installer runs all C1–C9 audits plus `--audit-bundle-c-completion`.

## Publish Result

- Installer publishes win-x64 to `Release\MajorMudReforged` and verifies `Reforged.Server.exe`.
