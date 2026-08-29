# Native NPC AI Semantics Exhaustion — Update 0324

PASS53 closes the broad ownership boundary from **UNKNOWN** to **STRONG** without claiming the unresolved native dispatcher as exact.

## Native ownership recovered

The retained WCCMMUD and WBBMMUD export surfaces each contain 592 exports and expose the same 43 selected NPC-AI boundary anchors across five owner families:

- 14 update-lane anchors (`fast`, `medium`, `slow`, `energy`);
- 9 target/admission anchors;
- 7 movement/follow/tracking anchors;
- 8 known-monster memory/confusion anchors;
- 5 monster combat/cast anchors.

These names establish dedicated native ownership boundaries. They do **not** establish instruction-equivalent WCC/WBB behavior, call order, timer phase, or branch semantics.

## Runtime disposition

Already recovered PASS/STRONG NPC behavior remains active: mixed-width monster records, Follow% field meaning, Pass-10 attack execution, explicit combat/damage threat ledger behavior, MonsGuards admission, lifecycle, and respawn boundaries. The deterministic Reforged scheduler and decision engine remain coordination infrastructure; their instance ordering, candidate priority, retry bookkeeping, and world-tick watermark receive no exact native Classic parity credit.

## Still native-dependent

Exact flee, autonomous perception/aggro, hate/threat switching and memory, patrol/path choice, tracking, assist timing/propagation, and fast/medium/slow/energy update ordering/cadence remain fail-closed. These prevent PASS.
