# Bundle C Revision 0001 — Equipment Authority Audit and Live Restriction Wiring

## DONE
- Audited live Classic equip/wear/wield execution against Bundle A character-side item authority.
- Removed the live restriction bypass: Classic equipment commands now load the single `RuntimeCharacterBundleCatalog` and pass it into `RuntimeItemEngine`.
- `RuntimeItemEngine.Equip` now consumes authoritative minimum level, class allowlist, race allowlist, and minimum-stat requirements from generated `items.json` through `RuntimeCharacterItemRequirementCatalog`.
- Draka's Blade (`item.1334`) now consumes the decoded Witchunter/MageBaneQuest entitlement and level-50 requirement before equipment mutation.
- Existing generated slot, strength, alignment, gender, hand-conflict, occupied-slot, broken-item, and cursed-removal enforcement remains in the same item runtime authority.
- Isolated/non-Classic callers retain a compatibility fallback; live Classic gameplay always supplies the authoritative character catalog.

## LEFT
- C2: resolve and promote the decoded class weapon-type and armour-type code relationship without guessing; current relationship remains fail-visible and is not invented.
- C2: trace any decoded ability, alignment, quest, gender, unique-owner, and use-only restrictions not represented by the current generated equipment projection.
- C3-C10 remain for later cumulative revisions.

## VERIFIED
- Item minimum-level, class, race, and minimum-stat inputs are consumed at live equip time.
- Draka's Blade requires Witchunter MageBaneQuest entitlement and level 50.
- Slot, strength, alignment, gender, two-handed, occupied-slot, broken-item, and no-remove checks execute in `RuntimeItemEngine`.
- Equipment state persists through the existing `RuntimeItemState` character save path after command execution.

## STRONGLY CORROBORATED
- None newly promoted in this revision.

## DECODED BUT NOT MAPPED
- Class weapon-type compatibility matrix.
- Class armour-type compatibility matrix.

## CONFLICTS
- The repository contains a dead name/type-based equipment-slot inference helper. It was not activated because Bundle C requires decoded-data authority and forbids invented gameplay.

## TRUE UNKNOWN
- None newly classified.

## Files Added
- `Librarium/Reports/bundle_c_revision_0001.md`
- `Updates/BundleCRevision0001/INSTALL_AND_VALIDATE.bat`

## Files Modified
- `Apps/Source/Reforged.Server/RuntimeItemsEquipment.cs`
- `Apps/Source/Reforged.Server/RuntimeLiveGameplayPart2.cs`

## Files Removed
- None.

## Duplicate Paths Removed
- Live Classic equip/wear/wield no longer bypasses Bundle A's authoritative item requirement catalog.

## Build Result
- Not executable in the Linux packaging environment because the .NET SDK is not installed. Installer performs Windows win-x64 restore/build with warnings treated as errors.

## Audit Result
- Installer runs Classic data, decode exhaustion, character bundle, combat bundle, item smoke, and Classic runtime audits.

## Publish Result
- Installer publishes to `Release\MajorMudReforged` and verifies `Reforged.Server.exe`.
