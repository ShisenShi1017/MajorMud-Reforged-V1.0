# Code Update 0281 — Classic Rooms / Doors / Locks / Traps Parity

## Scope

Pass 11 closes stale parity gaps in Classic room-action latches, semantic exits, canonical door-state ownership, and the exact decoded exit-trap corpus. It does not invent the remaining Picklocks, bash, generic trap-disarm, or unresolved hidden-exit semantics.

## Room-action latch closure

The seven edge states previously tracked as unresolved native/passive latches are no longer unresolved:

- `edge-state.map12.2051.u` — stone sphinx answer route. `ask sphinx stars`, `ask sphinx star`, and `ask sphinx the stars` follow NPC 550's decoded textblock chain to `remoteaction 2051 0 0 8`.
- `edge-state.map17.2946.s`
- `edge-state.map17.2949.e`
- `edge-state.map17.2956.n`
- `edge-state.map17.2958.n`
- `edge-state.map17.2970.s` — the five Map 17 boulder latches use `move boulder` / `push boulder`, a 5-second delay, Strength +60 d100 test, textblock 4235 failure branch, and exact retained remoteaction operands.
- `edge-state.map17.3042.n` — four exact pedestal actions consume diamond/pearl/moonstone/marble inventory records and execute remoteaction selectors 1..4 against action record 3042.

The retained living-world closure reports all **206/206 action gates bound**, with **zero native/passive placeholders** and **zero ordinary action-semantic gaps**.

## Native remoteaction owner

The state mutation path remains tied to the decoded native room-action executor at `wccmmud.dll!0x0046C573`. `WorldgroupRemoteActionDispatchRegistry` preserves the native selector/mask table and `RuntimeLivingWorldCompletion` consumes those semantics rather than maintaining a parallel guessed latch model.

## Semantic exit closure

The current 0183 semantic exit corpus contains **61,976 exits**:

- **61,975** `strongly_correlated_active`
- **1** `unknown_preserved_safe_runtime`

The old parity description claiming 266 unknown-qualified exits is retired.

The sole remaining qualifier gap is:

- `exit.map12.2248.d`, `12/2255 (Hidden/Unknown)` from the Golden Spire Secret Library to the Under the Palace Library.
- Its reciprocal `exit.map12.2255.u` is `Hidden/Searchable` and shares `edge-state.map12.2248.d`.
- Direct MDB command evidence binds `move rug` and `pull rug` to the downward exit.
- The exact native state mutation, repeatability, reset timing, and failure message are still unresolved, so the effect remains unactivated rather than guessed.

## Doors and locks

The existing live door runtime already proves the core decoded state path:

- one live door definition/state per decoded canonical exit identity;
- Classic decoded doors start closed;
- explicitly key-qualified doors start locked;
- movement blocks locked before closed;
- open/close/lock/unlock mutate persisted `RuntimeLivingWorldService` state;
- explicit generated key item IDs are enforced.

Still UNKNOWN: exact Picklocks and bash formulas, native key-consumption semantics without explicit evidence, exact command messages, unsupported broken/linked/remote door encodings, durability/recovery, and exact reset persistence for those unsupported variants.

## Classic exit traps

The decoded Classic exit-trap catalog is exact at **295** records:

- **273** fixed-damage exit traps;
- **22** spell exit traps.

The runtime preserves one trap per canonical exit. Fixed damage uses the authoritative defeat/death consumer when lethal. Spell traps route through `RuntimeSpellExecutionEngine`. No fake `DetectionDifficulty` or `DisarmDifficulty` values are introduced into the exact Classic exit-trap path.

The live command path explicitly resolves successful movement/departure, then the exit trap, then arrival/room rendering. That runtime ordering remains **STRONG** rather than PASS until the original native executable ordering is independently traced.

Still UNKNOWN under the broad native trap boundary: non-exit/container/item/room trap record families, detection/disarm formulas, failed-disarm behavior, exact messages, reset/rearm/charge semantics, persistence, and broader ordering across owners.

## Parity scoring

0281 scores **69 PASS / 45 STRONG / 50 UNKNOWN = 164 requirements**.

- Strict proven parity: **42.1%**
- Evidence-backed coverage: **69.5%**
- Rooms & World: **13 PASS / 3 STRONG / 6 UNKNOWN = 22**, strict **59.1%**, evidence-backed **72.7%**.

The denominator grew because previously broad room/door/trap behavior is now represented by independently testable requirements. The UNKNOWN count still falls from 51 to 50.

## Validation

`Tools/Validation/validate_pass11_rooms_doors_traps_parity.py` performs a no-generated-Data static audit against the retained Librarium/runtime corpus. Pass 11 currently passes **98/98 checks**.

The packaging environment does not contain the .NET SDK, so the Windows workstation build remains the final C# compile verification.
