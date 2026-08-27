# Code Update 0297 — Door Requirements / Trap State / Container Scope

Pass 26 sharpens the Classic room-action boundary from Pass 25 without inventing the unresolved native success dispatcher.

## Door / Key requirement correction

MajorMUD doors and keyed exits that expose PICKLOCK/BASH carry real action requirements. These requirements belong to the explicit `PICKLOCK <direction>` and `BASH <direction>` actions; they are not passive movement checks.

The retained 1.11p-derived room projection contains:

- 1,648 Door exits.
- 1,178 Doors with numeric Picklocks-or-Strength requirements.
- 109 Doors permitting any Picklocks/Strength value to attempt the action.
- 166 Key exits.
- 83 Key exits with a numeric Picklocks requirement plus a Strength alternative.
- 61 Key exits with a numeric Picklocks-only requirement.
- 1 Key exit permitting any Picklocks value.

The live action boundary now uses the character's recovered Picklocks value for PICKLOCK and Strength for BASH. A numeric projected requirement is enforced before the unresolved success dispatcher; an explicit `any` qualifier permits the attempt to reach that dispatcher. Normal traversal continues to depend only on the resulting door state.

The exact native success roll, raw Para2 arithmetic beyond the recovered display projection, exact messages, key-consumption behavior, durability/break behavior, and reset persistence remain evidence-gated.

## Key-exit Boolean parsing bug

The generated Key-exit semantic data stores `strength_alternative` as a Boolean. The live loader previously attempted to read that field as an integer, so `true` was discarded and some keyed exits silently lost their valid BASH/Strength alternative.

Pass 26 changes `StrengthAlternative` to Boolean in both the gate definition and Classic action-evidence record and loads it through `ReadBool`. Key exits such as `Key: 757 [or 81 picklocks/strength]` can now expose both the Picklocks and Strength action paths.

## Trap-state recovery

The native trap state family remains recovered as:

0. Active
1. Inactive
2. Hidden
3. Active movement
4. Inactive movement

The retained semantic projection contains 295 decoded exit traps but discarded each record's native Para2 state. A public stock `data-v1.11p.mdb` was located in the MMUD Explorer source repository, but the available connector in this environment cannot decode the binary MDB. No trap-state values are guessed.

`DISARM TRAP <direction>` remains recognized, while exact detection/disarm rolls, failed-disarm behavior, state transitions, rearming and reset behavior remain UNKNOWN.

## Generic container scope correction

No standalone stock RoomType container family or generic Classic container service has been recovered. The synthetic `native.world-containers` requirement is therefore removed as a duplicate parity bucket rather than implemented as an invented subsystem.

Actual MajorMUD storage/loot behavior remains owned by the systems that provide it:

- Character/Items — carried and nested item ownership and death piles.
- Economy — bank/currency storage.
- Gangs — gang-house vault/ownership behavior.
- NPC/Spawn/Loot — named treasure-chest content and spawning.

`RuntimeWorldContainerService` may remain as a Reforged extension abstraction, but it is not credited as a Classic subsystem.

## Score

- PASS: 73
- STRONG: 76
- UNKNOWN: 28
- Total: 177
- Strict parity: 41.2%
- Evidence-backed: 84.2%
