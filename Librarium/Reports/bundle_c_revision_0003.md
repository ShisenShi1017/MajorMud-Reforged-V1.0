# Bundle C Revision 0003 — Phase C2 Equipment Restrictions

## DONE
Phase C2 is complete. All decoded and promotable equipment restrictions now have one authoritative owner, live consumer, persistence disposition, and validation. Live equip/wear/wield enforces canonical slot, occupied-slot and hand conflicts, minimum level, decoded class and race allowlists, minimum statistics/strength, alignment and gender hooks, and the verified Draka Blade Witchunter quest entitlement. The duplicate instance-metadata class/race allowlist path was removed; Bundle A item requirements are the sole Classic class/race authority.

The audit inventories 1,950 item records and 532 equipment relationships and classifies every requested restriction family. Limited-item ownership is validated by the existing global persistence reconciliation coordinator. Creation and loot-side limit production remains assigned to Phase C8 rather than duplicated here.

## LEFT
Phase C3 — complete equipment runtime: equip/unequip state effects, all decoded modifiers, weight/encumbrance interaction, visibility, hidden behavior, and persistence validation.

## VERIFIED
Slots, occupied-slot rules, hand conflicts, minimum level, class allowlists, race allowlists, minimum stats, Draka Blade Witchunter entitlement, and limited-item persistence validation.

## STRONGLY CORROBORATED
Alignment and gender restriction consumers. They are active and data-driven, but generated Classic equipment currently contains no populated records.

## DECODED BUT NOT MAPPED
Weapon-type and armor-type class compatibility codes are decoded on both class and item records, but exhaustive repository search found no relationship matrix. They remain explicitly relationship-unresolved and are not guessed.

## CONFLICTS
The generated equipment `class_restricted` boolean was previously paired with per-instance metadata allowlists. That duplicate path conflicted with Bundle A's decoded item class list and has been removed from live execution.

## TRUE UNKNOWN
The native weapon/armor code relationship remains a true relationship unknown pending later native evidence.

## Files Added
- `Apps/Source/Reforged.Server/RuntimeBundleCEquipmentRestrictionAudit.cs`
- `Librarium/Reports/bundle_c_revision_0003.md`

## Files Modified
- `Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs`
- `Apps/Source/Reforged.Server/Program.cs`
- `INSTALL_AND_VALIDATE.bat`

## Files Removed
None.

## Duplicate Paths Removed
Per-instance `allowed_classes` and `allowed_races` metadata checks were removed from live equip. `RuntimeCharacterItemRequirementCatalog` is now the only Classic class/race restriction evaluator.

## Build Result
Not executable in the packaging environment because the .NET SDK is unavailable. The installer requires zero warnings and zero errors.

## Audit Result
Static restriction audit passed. Installed validation runs `--audit-bundle-c-equipment-restrictions` in addition to all prior Bundle C and project audits.

## Publish Result
Performed by `INSTALL_AND_VALIDATE.bat` to `Release\MajorMudReforged`; success requires `Reforged.Server.exe`.
