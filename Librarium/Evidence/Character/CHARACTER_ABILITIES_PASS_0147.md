# Character Abilities Pass 0147 — Quest/Script Value Lifecycle

## Promoted

- `giveability ID VALUE` sets the character-owned ability value exactly.
- `addability ID DELTA` adds with checked integer arithmetic.
- `checkability` is exact equality, `testability` is greater-than-or-equal, and `failability` is zero.
- Clause execution is ordered and atomic: failure restores the pre-clause snapshot.
- Values persist in `RuntimeCharacterRecord.Conditions` under `quest.ability.<legacy_id>`.
- 2500 opcode occurrences across 364 script fields reference 9 distinct canonical abilities; all references resolve.

## Not promoted

Trainer acquisition, automatic level grants, temporary lifetime, cooldowns, activation behavior, and ability-specific numeric meanings remain unresolved.
