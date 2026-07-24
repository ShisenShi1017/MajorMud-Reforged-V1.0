# Bundle C Revision 0009 — Phase C8 Loot

## DONE
- Completed decoded NPC item and currency loot ownership through the live defeat consumer.
- Preserved copper, silver, gold, platinum, and runic as exact authored amount fields; currency is never interpreted as a drop percentage.
- NPC item drops consume decoded canonical item relationships and decoded drop percentages.
- Replaced process-global random loot rolls with deterministic per-kill/per-item rolls while retaining the authored percentage contract.
- Existing NPC-carried items and generated loot materialize as persistent room-ground item state.
- Limited ownership, stacking, cleanup deletion/hiding, hidden search, and per-player discovery remain owned by the authoritative item and room-ground runtimes.
- Added `--audit-bundle-c-loot`.

## LEFT
- Phase C9 Persistence.

## VERIFIED
- 1,100 generated NPC definitions are the live NPC reward source.
- 368 NPC definitions contain authored non-zero denomination amounts.
- 420 NPC definitions declare known loot tables.
- Generated NPC currency fields are amounts, not chance fields.
- Generated item loot uses canonical item identities and percentage rolls.
- Room-ground items and currency enter persistence and cleanup/search consumers.

## STRONGLY CORROBORATED
- A decoded item drop percentage of zero never awards the item; 100 always awards it.
- Existing NPC inventory transfers to the room on defeat in addition to decoded generated loot.

## DECODED BUT NOT MAPPED
- None for executable NPC currency amounts or decoded NPC item-drop relationships.

## CONFLICTS
- Removed process-global `Random.Shared` as the live NPC loot selector.
- No currency amount is treated as a percentage chance.

## TRUE UNKNOWN
- Native variable item quantity ranges are absent from the executable generated NPC relationship and are not invented; decoded generated drops materialize one item.
- No separate executable rare, random-table, or quest-drop classification exists beyond decoded NPC item probabilities; rarity and quest ownership are not inferred from percentage alone.
- Native limited-item global release ordering remains deferred to the final native pass; existing authoritative ownership limits fail closed.

## Files Added
- `Apps/Source/Reforged.Server/RuntimeBundleCLootAudit.cs`
- `Librarium/Reports/bundle_c_revision_0009.md`

## Files Modified
- `Apps/Source/Reforged.Server/RuntimeCombatCompletion.cs`
- `Apps/Source/Reforged.Server/Program.cs`
- `INSTALL_AND_VALIDATE.bat`

## Files Removed
- None.

## Duplicate Paths Removed
- Process-global live NPC loot random path.

## Build Result
- Not executable in the packaging environment because the .NET SDK is unavailable.
- Installer requires `dotnet build -warnaserror` with zero warnings and zero errors.

## Audit Result
- Static source/data audit passed for generated NPC reward fields, canonical item resolution path, live defeat consumption, limited ownership, cleanup, hidden search, and persistence ownership.
- Runtime loot audit is included in the installer.

## Publish Result
- Installer publishes win-x64 to `Release\MajorMudReforged` and verifies `Reforged.Server.exe`.
