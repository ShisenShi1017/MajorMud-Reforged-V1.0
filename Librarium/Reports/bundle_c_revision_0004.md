# Bundle C Revision 0004 — Phase C3 Equipment Runtime

## DONE

Phase C3 is closed end-to-end. Live equip, wear, wield, remove, and unequip operations continue to execute only through `RuntimeItemEngine`, then commit through one `RuntimeEquipmentRuntimeAuthority` path. The commit atomically persists item locations, the character equipment-slot map, root inventory identities, decoded equipment modifiers, derived combat statistics, resistance projection, carried weight, runtime provenance, and validation.

Decoded `abilities_named` values are loaded for equipment and aggregated once by `RuntimeItemEngine.EquipmentModifierTotals`. Verified direct numeric equipment modifiers are consumed by `RuntimeCharacterCombatStatisticProjector`; unknown ability semantics remain represented in the authoritative modifier map without invented gameplay.

Slot occupancy, legal-slot matching, two-hand/off-hand conflicts, broken-item rejection, no-remove/cursed removal restrictions, authoritative Bundle A requirements, and Draka entitlement remain enforced by the single item engine.

## LEFT

Phase C4 — Items.

## VERIFIED

- Equip/wear/wield and remove/unequip share one runtime owner.
- Equipment slot and hand validation execute before state mutation.
- Equipment item locations and character equipment map persist together.
- Armor class, damage resistance, accuracy, speed, known resistance, regeneration, and ability modifiers are recalculated from equipped instances.
- Carried weight includes character, equipment, and nested-container ownership and is persisted after equipment transitions.
- Character persistence validation runs before save.
- Equipment source instance IDs and Bundle C contract version are persisted for provenance.

## STRONGLY CORROBORATED

- Numeric `abilities_named` entries on equipped items are additive equipment modifiers. They are aggregated data-driven; only fields with an existing verified runtime consumer alter gameplay.

## DECODED BUT NOT MAPPED

None within Phase C3. Ability IDs lacking a verified semantic consumer remain preserved in the authoritative modifier map and are classified relationship-unresolved, not discarded.

## CONFLICTS

None.

## TRUE UNKNOWN

The native carry-capacity formula, encumbrance thresholds, and exact movement/accuracy/swing penalties remain unresolved by available decode. C3 persists proven total carried weight and does not invent those formulas. Their final consumer remains assigned to the inventory/economy exhaustion work when native evidence is established.

## Files Added

- `Apps/Source/Reforged.Server/RuntimeBundleCEquipmentRuntime.cs`
- `Librarium/Reports/bundle_c_revision_0004.md`

## Files Modified

- `Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs`
- `Apps/Source/Reforged.Server/RuntimeCharacterDerivedStatistics.cs`
- `Apps/Source/Reforged.Server/RuntimeLiveGameplayPart2.cs`
- `Apps/Source/Reforged.Server/Program.cs`
- `INSTALL_AND_VALIDATE.bat`

## Files Removed

None.

## Duplicate Paths Removed

The item-state-only save path after live equipment mutation was removed. Both equipment command surfaces now use the same authoritative equipment commit path.

## Build Result

Not executable in the packaging environment because the .NET SDK is unavailable. The installer requires restore and a zero-warning/zero-error Release win-x64 build.

## Audit Result

Static Phase C3 audit passed. The installer runs all prior audits plus `--audit-bundle-c-equipment-runtime` and item/runtime smoke validation.

## Publish Result

Performed by `INSTALL_AND_VALIDATE.bat` to `Release\MajorMudReforged`; it verifies `Reforged.Server.exe` before success.
