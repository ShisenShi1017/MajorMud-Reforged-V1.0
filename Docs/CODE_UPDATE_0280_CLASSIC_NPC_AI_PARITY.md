# Code Update 0280 — Classic NPC / AI parity

## Scope
Pass 10 narrows the broad NPC/AI UNKNOWN without guessing unresolved perception, hate/threat, assist, flee, patrol/pathing, or retry-priority semantics. It activates only behavior supported by retained MMUD Explorer / Nightmare Redux attack-simulator evidence and decoded Classic monster rows.

## Recovered NPC combat behavior
- `attack_count` is the number of authored executable attack rows, not the number of attacks made per round.
- An attacking NPC receives six row-selection attempt slots per 5-second combat round.
- Monster `Energy` is energy gained per round. Carried energy starts at zero, the decoded value is added at each round, and unused energy carries forward without a one-round cap.
- Each attempt makes one inclusive d100 roll and scans attack slots in authored order. The first row whose `Att%`/`selection_percent` threshold is at least the roll is selected. Unaffordable rows are not removed and the remaining rows are not re-normalized.
- Type-2 direct spell rows overload the monster attack fields: `AttAcc` is spell id, `AttMax` is cast level, and `AttMin` is cast-success percent. A separate inclusive d100 determines cast success.
- A failed or fully resisted direct Type-2 cast consumes `Round(AttEnergy/2, 0)` with VB midpoint-to-even rounding. Physical attack misses/dodges and resisted physical hit-spells retain full physical attack cost.
- The retained simulators contain a Type-1 physical early-end branch when remaining energy falls below the selected physical row's energy cost.

## Evidence classification
These NPC cadence/selection rules are **STRONG**, not PASS. MMUD Explorer and Nightmare Redux independently contain matching recovered implementations, and decoded monster field usage agrees with those implementations, but the original native MajorMUD executable call chain has not yet been traced end-to-end.

## Operator-confirmed Classic boundaries folded into the parity ledger
- MajorMUD exposes one Sysop role; Worldgroup security keys (for example SYSOP/USER/DEMO-class keys) gate MajorMUD sysop commands through `WCCMMUD.INI`. Exact command-key mapping still needs native/INI exhaustion.
- MajorMUD has no native in-game content builder. Content editing belongs to external tooling such as Nightmare Redux. This exclusion is STRONG pending final negative native command-table proof.
- Classic lives: players start with / cap at 9 lives; death costs one life; training/level-up grants a life up to the cap; zero lives leads to permanent character death. Native help text independently supports the 9-life start/cap, death cost, level-training life gain, and permanent death boundary. The exact +1 increment is operator-confirmed but remains STRONG until native code/table proof.
- Death XP loss, floor-overflow spill ordering, and alignment/outlaw respawn routing are kept separately scored so unresolved exact formulas do not hold the entire lives system UNKNOWN.

## NPC HP regeneration evidence
- MMUD Explorer retains the stock constant `STOCK_MOB_HPREGEN_ROUNDS = 18`.
- The decoded NPC catalog retains each monster's authored `hp_regen` amount.
- Exact native tick phase/order relative to incoming damage, death resolution, idle/out-of-combat state, and restart recovery is not yet traced. Pass 10 therefore records the interval/amount inputs as **STRONG** and deliberately does not guess the live regeneration consumer order.

## Still UNKNOWN after this pass
- Native NPC perception and acquisition rules.
- Exact native hate/threat ownership and target-switch ordering.
- Native assist/group-guard semantics beyond currently implemented deterministic Reforged behavior.
- Native flee decision thresholds and path choice.
- Native patrol/pathing and retry/action priority cadence.
- Native caller-owned arguments for combat message 8480.
