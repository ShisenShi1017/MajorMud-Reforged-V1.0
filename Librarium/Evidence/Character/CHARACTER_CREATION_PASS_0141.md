# Character Creation Semantic Pass 0141

CodeUpdate0141 promotes the already decoded Classic creation foundation into an explicit runtime and evidence contract.

## Closed in this pass

- 13 promoted races and their six-stat minimum/maximum ranges.
- 15 promoted classes and their HP, magic, weapon, armour, combat, and ability fields.
- All 195 race/class combinations are allowed; the declared and computed matrices match.
- New characters begin at level 1 with zero experience.
- Starting statistics are the selected race minimum statistics.
- Starting alignment is Neutral (`0`) with zero evil points.
- Starting gold, inventory, equipment, and spells are empty/zero.
- Starting abilities are the race/class ability-slot union, with duplicate IDs resolved once.
- Starting location is `room.map1.2140`, room 2140, Newhaven Village Entrance.
- Initial HP and mana continue to use the promoted DLL-derived formulas from pass 0137.

## Runtime impact

`RuntimeCharacterFoundation.cs` now reads and validates the promoted creation contract instead of embedding independent assumptions. Creation fails safely if the 195-pair matrix or generated start room diverges. Race creation-point and experience modifiers are retained in the saved initial character conditions for traceability.

## Remaining dependency

The exact native interactive ability-generation/point-allocation workflow is not promoted. Runtime therefore continues to use the proven deterministic minimum-stat creation state and rejects unsupported custom stat selection rather than inventing point-buy rules.
