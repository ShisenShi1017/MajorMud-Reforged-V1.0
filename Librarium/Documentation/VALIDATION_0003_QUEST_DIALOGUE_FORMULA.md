# Validation-0003 — Quest, Dialogue, and Formula Verification

Generated: 2026-07-07T23:31:16.966331+00:00

Result: **PASS_WITH_FINDINGS**

This pass combined the three requested validation domains into one milestone:

1. Quest validation
2. Dialogue/textblock validation
3. Formula verification

## Computed metrics

- Quest/quest-item abilities: 12
- Watched hybrid abilities: 1
- Item→ability relationships: 4546
- Spell→ability relationships: 3889
- Monster→ability relationships: 2293
- Quest candidates carried forward: 12
- Canonical quest records: 0
- Textblock references: 16
- Dialogue references: 16
- Formula models evaluated: 10

## Promotion decision

Canonical `/Data` was not overwritten. `Data/quests.json` remains unchanged because ordered quest stages still require stronger decoded NPC/textblock ordering evidence.

Direct item/spell/monster ability relationships are verified relationship facts and are now available as validation outputs. Exact classic numeric formulas remain Derived until numeric calculator/runtime/reference evidence proves them.
