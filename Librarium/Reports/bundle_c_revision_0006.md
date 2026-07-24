# Bundle C Revision 0006 — Phase C5 Inventory

## DONE

Phase C5 is closed for all promoted inventory relationships. Character inventory, equipped items, nested carried containers, physical currency denominations, carried-weight projection, inventory/equipment slot projection, save/load ownership, and live inventory rendering now use one Bundle C inventory authority.

Room currency pickup preserves copper, silver, gold, platinum, and runic denomination quantities in `RuntimeItemState.ActiveItemState` while retaining the existing copper-base compatibility total for older economy consumers. Currency is projected as physical carried inventory and contributes to carried weight. Item weight includes character inventory, equipment, and nested owned containers.

The prior live `strength * 80` capacity formula and guessed percentage bands were removed because no decoded/native relationship supports those values.

## LEFT

Phase C6 — Merchants.

## VERIFIED

- Inventory and equipment are persisted in `RuntimeCharacterRecord.ItemState`.
- Item weight is decoded per item and multiplied by stack quantity.
- Equipped and nested-container items remain carried ownership.
- All five currency denominations are physical character holdings and persist independently.
- Currency changes update carried-weight projection.

## STRONGLY CORROBORATED

- One physical coin contributes one inventory weight unit pending recovery of a denomination-specific native ratio.
- Existing copper-base totals remain a compatibility projection only; denomination quantities are authoritative for inventory ownership.

## DECODED BUT NOT MAPPED

None among promoted Phase C5 fields.

## CONFLICTS

The former live inventory display invented an 80-minimum / strength-times-80 capacity formula and five percentage bands. It conflicted with prior Librarium evidence that the native capacity formula and thresholds were unresolved. That path was removed.

## TRUE UNKNOWN

- Exact native carrying-capacity formula.
- Exact native No/Light/Medium/Heavy thresholds.
- Exact attack speed, accuracy, and movement penalties by encumbrance band.
- Any denomination-specific coin weight ratio different from one physical weight unit per coin.
- A decoded fixed maximum count of root inventory entries; no artificial slot cap is enforced.

## Files Added

- `Apps/Source/Reforged.Server/RuntimeBundleCInventoryAuthority.cs`
- `Librarium/Reports/bundle_c_revision_0006.md`

## Files Modified

- `Apps/Source/Reforged.Server/RuntimeClassicRoomCurrency.cs`
- `Apps/Source/Reforged.Server/RuntimeLiveGameplay.cs`
- `Apps/Source/Reforged.Server/RuntimeLiveGameplayPart2.cs`
- `Apps/Source/Reforged.Server/Program.cs`
- `INSTALL_AND_VALIDATE.bat`

## Files Removed

None.

## Duplicate Paths Removed

- Gold-only room currency pickup collapse.
- Independent live inventory weight calculation.
- Invented live carry-capacity and encumbrance-band calculation.

## Build Result

Not executable in the packaging environment because the .NET SDK is unavailable. Installer requires `-warnaserror` and fails on any build warning or error.

## Audit Result

Static source audit passed. Installer runs `--audit-bundle-c-inventory` plus all prior Bundle A, Bundle B, and Bundle C audits.

## Publish Result

Installer publishes win-x64 to `Release\MajorMudReforged` and verifies `Reforged.Server.exe`.
