# Capacity and Resource Limits — Update 0327

PASS56 promotes `native.capacity-resource-limits` from **UNKNOWN** to **STRONG** by separating exact retained MajorMUD capacity/default evidence from still-unrecovered hard maxima.

## Exact retained capacity/default evidence

WCCMMUD.mcv retains exact defaults for rooms (1500), items (750), active monsters (2000), known monsters (300), spells (150), races (12), classes (16), bankbooks (32), three message buffers (250/50/10), TOPTEN list maximum (30), and maximum items lost on hangup (0). The buffer values are configuration defaults and are **not** promoted as hard world/data maxima.

Already recovered owning subsystems also provide narrow caps/widths: maximum lives 9, party size 6 (STRONG), gang-name maximum 19 characters, native UserRec first/family-name storage widths 10/18 bytes, MajorMUD Plus room capacities 17 visible / 15 hidden and 50 placement attempts, plus the stock STR-based maximum encumbrance formula.

## Reforged generic coordinator

`RuntimeCapacityLimitsService` remains a useful extension shell. Its configurable `long` maxima, reserve/commit/release counters, owner strings, and synthetic failure codes receive **zero Classic parity credit**. It is not evidence that Classic uses reservations or Int64 counters.

## Still native-dependent

The exact carried inventory slot/item-count limit, room-floor object count and overflow ordering, world item/object-instance maximum, NPC hard maximum, gang member cap, character-slot cap, hard world/data table maxima, remaining numeric widths/signedness, configuration ranges/live-mutability rules, allocation-exhaustion cleanup/retry semantics, and exact player-visible failure messages remain fail-closed. These prevent PASS.
