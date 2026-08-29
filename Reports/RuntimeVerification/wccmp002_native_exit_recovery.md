# WCCMP002 Native Exit Recovery — Code Update 0333

- Source SHA-256: `06eec04e1020ded5f63d6a9e67a794b871696ca90f6afc0fcdfcccdd4ac657be`
- Validated room record offsets: **26,666**
- Canonical exits checked: **61,976**
- Native destination matches: **61,956**
- Known cross-version/legacy destination mismatches retained without overwrite: **20**
- Canonical traps recovered: **295** (**273 damage + 22 spell**)
- Trap Para2 states: **293 Active (0)**, **0 Inactive (1)**, **2 Hidden (2)**, **0 Active movement (3)**, **0 Inactive movement (4)**

The exact native Para2 value is now attached to every canonical Classic trap as `effects.native_trap_state`. Runtime effect execution remains fail-closed because exact SEARCH/DISARM rolls, failed-disarm triggering, state transitions, reset/rearm behavior, and simultaneous movement/death ordering are separate unresolved behaviors.

## Native/canonical destination review queue

The 20 destination mismatches are retained as evidence and are **not** auto-overwritten. They include native zero/unlinked slots and cross-version/legacy destination differences that require owner-specific review.

- `exit.map1.207.w`: canonical room 2668 vs native RoomExit 0 (RoomType 7, Para1 2, Para2 0, Para3 0, Para4 0)
- `exit.map1.2447.s`: canonical room 2448 vs native RoomExit 0 (RoomType 0, Para1 0, Para2 0, Para3 0, Para4 0)
- `exit.map1.2448.n`: canonical room 2447 vs native RoomExit 0 (RoomType 0, Para1 0, Para2 0, Para3 0, Para4 0)
- `exit.map1.2471.s`: canonical room 2472 vs native RoomExit 0 (RoomType 0, Para1 0, Para2 0, Para3 0, Para4 0)
- `exit.map1.2472.n`: canonical room 2471 vs native RoomExit 0 (RoomType 0, Para1 0, Para2 0, Para3 0, Para4 0)
- `exit.map1.2493.n`: canonical room 2494 vs native RoomExit 0 (RoomType 0, Para1 0, Para2 0, Para3 0, Para4 0)
- `exit.map1.2494.s`: canonical room 2493 vs native RoomExit 0 (RoomType 0, Para1 0, Para2 0, Para3 0, Para4 0)
- `exit.map1.2495.s`: canonical room 2496 vs native RoomExit 0 (RoomType 0, Para1 0, Para2 0, Para3 0, Para4 0)
- `exit.map1.2496.e`: canonical room 2517 vs native RoomExit 0 (RoomType 0, Para1 0, Para2 0, Para3 0, Para4 0)
- `exit.map1.2496.n`: canonical room 2495 vs native RoomExit 0 (RoomType 0, Para1 0, Para2 0, Para3 0, Para4 0)
- `exit.map1.2497.e`: canonical room 2516 vs native RoomExit 0 (RoomType 0, Para1 0, Para2 0, Para3 0, Para4 0)
- `exit.map1.2498.e`: canonical room 2515 vs native RoomExit 0 (RoomType 0, Para1 0, Para2 0, Para3 0, Para4 0)
- `exit.map1.2499.e`: canonical room 2514 vs native RoomExit 0 (RoomType 0, Para1 0, Para2 0, Para3 0, Para4 0)
- `exit.map1.2514.w`: canonical room 2499 vs native RoomExit 0 (RoomType 0, Para1 0, Para2 0, Para3 0, Para4 0)
- `exit.map1.2515.w`: canonical room 2498 vs native RoomExit 0 (RoomType 0, Para1 0, Para2 0, Para3 0, Para4 0)
- `exit.map1.2516.w`: canonical room 2497 vs native RoomExit 0 (RoomType 0, Para1 0, Para2 0, Para3 0, Para4 0)
- `exit.map1.2517.w`: canonical room 2496 vs native RoomExit 0 (RoomType 0, Para1 0, Para2 0, Para3 0, Para4 0)
- `exit.map1.2668.e`: canonical room 207 vs native RoomExit 210 (RoomType 7, Para1 2, Para2 0, Para3 0, Para4 0)
- `exit.map12.2252.e`: canonical room 2369 vs native RoomExit 2376 (RoomType 6, Para1 1, Para2 0, Para3 1, Para4 1)
- `exit.map12.2252.w`: canonical room 2369 vs native RoomExit 2370 (RoomType 6, Para1 1, Para2 0, Para3 1, Para4 1)
