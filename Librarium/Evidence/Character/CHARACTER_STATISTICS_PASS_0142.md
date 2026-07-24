# Character Statistics Pass 0142

## Promoted runtime behavior

This pass isolates the proven Classic creation-time HP and mana calculations in a dedicated runtime calculator. The source projection is `Data/Classic/character_foundation.json`, backed by `wccmmud.dll` function `calculate_secondary_stats` at `0x0041a424` and the prior Character HP/Mana evidence record.

Maximum HP evaluates in this exact order:

`HEA/2 + level*class minimum HP + level*race HP adjustment + con_adj + accumulated HP rolls + con_adj + MaxHP ability`

`con_adj` uses `(HEA-50)*level`; negative products add 15 before signed arithmetic shift right by 4. The duplicate addition of `con_adj` is preserved because it is observable in the native routine. Creation seeds accumulated HP rolls with the class maximum bonus-per-level cap. Final HP is clamped to at least 1.

Mana uses class magic type: type 0 or no magic gives 0; types 1-4 use `MaxMana ability + magic level*level*2 + equipment cache + 6`; type 5 uses `level-1`. Negative mana is clamped to zero. Arithmetic is checked and fails safely on overflow.

## Scope boundary

This pass does not invent AC, attack, defense, initiative, speed, resistance, save, SP/EP, or regeneration formulas. Those remain explicit Character-statistics dependencies for later passes or the final native pass.
