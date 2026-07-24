# Combined Decode Audit After NPC and AI Merge

## Merge result
- Base files: 1196
- NPC/AI archive entries: 237
- Four shared source paths were semantically reconciled; neither subsystem variant was silently overwritten.

## Remaining decode work

### 1. Items
A dedicated Items cumulative archive has not yet been merged. Item definitions, instances, ownership, stacking, charges, durability, use behavior, portable containers, limited-item rules, reset/restock, death/corpse transitions, and persistence remain the largest unclosed subsystem.

### 2. NPC and AI non-native closure
The package reports `exit_criteria_met: false`. Remaining executable work:
- merchant restock cadence, pricing modifiers, merchant gold, and native restriction derivation
- NPC equipment ranking, switching timing, and comparison policy
- spawn chance, native population maxima and dynamic group selection
- native perception rule decoding and hostility classification
- native threat formulas, taunt, hate transfer and chase decisions
- patrol, wandering, chase, leash-trigger decisions, multi-step return-home routing and pathing
- merchant stock/restock persistence and trainer offering-state persistence beyond exact request ownership
- remaining administrative inventory, service-state and ownership mutation controls
- companion command handling, automatic lifecycle event detection, despawn integration and owner-session policy

### 3. NPC and AI native closure
- native spawn population algorithm
- failed-respawn retry cadence
- hard reset exceptions
- perception masks
- threat and path selection branches
- sight/hearing ranges and stealth detection rolls
- hostility classification and target memory duration
- damage/healing/spell threat formulas
- threat decay and taunt semantics
- assist propagation and target memory
- damage, healing and spell threat formulas
- native threat decay cadence and percentage
- native threat memory duration and lost-target timeout
- taunt strength, duration, resistance, cooldown and break conditions
- hate-transfer semantics
- native chase and leash decisions

### 4. Existing subsystem native boundaries
- Character native lifecycle/session boundary.
- Textblock/command/ability native message, dispatch, parser, and remote-action boundary.
- Economy native stage-order and executable binding boundary.
- Combat/spell native record, formula, timing, flag, and save boundary.
- Remaining Systems native behavior and Worldgroup ownership boundary.
- Room-native latch/exit closure carried forward unless superseded by verified native evidence.

### 5. Final native pass
After Items and the remaining NPC/AI executable work are closed, perform the consolidated WCC/WBB/native DLL/file pass.
