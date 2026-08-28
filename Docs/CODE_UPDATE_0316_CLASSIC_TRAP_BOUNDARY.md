# Code Update 0316 — Pass 45 Classic Trap Boundary

## Parity movement

- `native.traps`: **UNKNOWN → STRONG**
- Score: **97 PASS / 86 STRONG / 17 UNKNOWN = 200**
- Strict parity: **48.5%**
- Evidence-backed parity: **91.5%**

## Exact native help connected

- WCCMMHLP **15 / HL1B4B**: normally anyone can find a trap by SEARCH; the Traps skill gives a much higher chance; a traps expert may attempt to disarm a trap found.
- WCCMMHLP **245 / HL1B106**: a character with the Traps skill may attempt to disarm a trap only **after it is found with SEARCH**, using `DISARM TRAP <direction>`.

These payloads close command/discovery **admission semantics**, not the hidden arithmetic. No SEARCH or DISARM probability is inferred from the words “higher chance.”

## Strong retained room/trap boundary

- 295 canonical Classic exit traps are retained: **273 fixed-damage + 22 spell traps**.
- RoomRec type 09 owns damage traps; type 24 owns spell traps.
- Para2 is a signed 16-bit trap-state field with the recovered family: 0 Active, 1 Inactive, 2 Hidden, 3 Active movement, 4 Inactive movement.
- The MMExplorer semantic projection retained identity/effect operands but discarded each exit's native Para2 value.

## Runtime policy

The existing Classic runtime remains deliberately fail-closed. `DISARM TRAP` enforces the Traps-skill gate and does not reuse the generic hidden-object SEARCH roll. `RuntimeClassicExitTrapCatalog` accepts only a future explicit `native_trap_state` projection with a known 0-4 value, but trap execution remains disabled until exact state-transition semantics are recovered. The generic `RuntimeTrapService` is a Reforged extension and is not evidence for Classic charges, rearm timers, or disarm formulas.

## Still unresolved

- exact per-exit WCCMP002 Para2 values
- exact SEARCH probability and Traps-skill modifier
- exact DISARM probability
- failed-disarm trigger behavior
- Para3/Para4 message ownership and exact rendering
- exact transitions for Active/Inactive/Hidden/Active-movement/Inactive-movement
- reset/rearm/persistence behavior
- exact simultaneous movement/trap/death/reset ordering

## Nahr 8480 target

Pass 45 checked `npc.native-combat-message-8480` first. It remains UNKNOWN: the retained Nahr row proves five hit phrases and message ID 8480, but still does not carry the caller-owned `miss_verb3` and `weapon_name` arguments. No miss wording was inferred from hit prose.

## Protected boundaries

- no `WCCMMUD.INI`
- Borin Trainer unchanged
- Custom1 Simple Editor unchanged
- WPF/UI fixes unchanged
- Pass 40 Shadow LOOK preserved
- Pass 43 event boundary preserved
- Pass 44 party boundary preserved
