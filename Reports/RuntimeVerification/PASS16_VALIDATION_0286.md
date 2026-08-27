# Pass 16 Validation — Native NPC AI / 16-bit Record Closure (0286)

## Result

**PASS**

The dedicated static validator `Tools/Validation/validate_pass16_native_npc_ai.py` passes **36/36** checks.

## Verified in this pass

- Classic NPC corpus remains exactly 1,100 records.
- Native mixed-width monster storage contract is represented explicitly rather than globally coercing all values to 16-bit.
- Native `Follow` is retained as an Int16 `Follow%` field; all retained Classic values are 0..100.
- Native attack selection is retained as a byte. Executable values 200 and 232 are preserved and are not clamped to 100.
- Ability 146 `MonsGuards` projects 112 owner NPCs / 159 relationships, and every guarded legacy id resolves to a Classic NPC.
- Classic live guard admission is isolated to decoded ability 146. Reforged assist instance/group links are non-Classic extensions.
- Message 8480 is proven as a physical dodge/miss template, not an assist broadcast.
- Nine physical Classic rows use 8480: four Thrag rows are fully bound; five Nahr (`npc.1006`) rows remain evidence-blocked.
- The parity ledger keeps both broad native AI selection and the remaining Nahr 8480 callsite argument gap UNKNOWN.

## Regression checks

The following validators complete successfully after the Pass 16 changes:

- Pass 10 NPC parity
- Pass 11 Rooms / Doors / Traps
- Pass 12 Economy / Transactions / Services
- Pass 13 Character Progression native closure
- Pass 14 Worldgroup ownership scope
- Pass 15 Death / Law / PvP / lifecycle
- Pass 16 Native NPC AI
- Combat runtime
- Combat runtime part 2
- Economy native markup
- Gameplay Group 4

## Build limitation

The current assistant container does not have the .NET SDK (`dotnet` is not installed), so the final C# Release compilation must still be performed in the user's Windows repository.

## Score

- PASS: 72
- STRONG: 59
- UNKNOWN: 47
- Total: 178
- Strict: 40.4%
- Evidence-backed: 73.6%
