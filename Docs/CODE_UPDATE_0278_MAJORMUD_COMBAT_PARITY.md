# Code Update 0278 - MajorMUD Combat Parity

## Scope

This cumulative pass advances stock MajorMUD physical-combat parity without widening unresolved native behavior. It starts from Code Update 0277 / cumulative Pass 7 and preserves the elemental over-resistance healing merge.

## Proven timing split

Recovered MMUD Explorer/native-correlated constants separate two clocks:

- physical combat round: **5 seconds**
- spell/system round: **3 seconds**

The host combat scheduler now defaults to 5000 ms. Spell/status duration and periodic-effect scheduling no longer borrow the physical-combat clock; decoded spell durations use three-second spell rounds. Existing saved active effects that carry the earlier duration-unit metadata are normalized onto the spell-round authority.

## Physical hit / avoidance corrections

Stock physical attacks now use the recovered hit/AC equation:

`100 - ((Defense * Defense) / max(1, (Accuracy * Accuracy) / 140))`

The result is clamped to **8%..99%**. A d100 roll is inclusive: `roll <= chance` succeeds. This fixes the prior off-by-one behavior that made a nominal 99% hit chance behave as 98%.

Stock Dodge uses the recovered divisor/cap behavior, including the 95% cap, accuracy threshold, Backstab `/5` reduction, and inclusive percentile roll.

## Attack energy and swing scheduling

The player weapon-energy authority now executes the recovered stock branches for:

- base weapon energy from class combat code, level, weapon speed, and Agility;
- below-weapon-STR-requirement energy penalty;
- actual carried-weight encumbrance percentage using the recovered capacity formula;
- 1000 action-energy units per physical combat round;
- unused-energy carry-forward;
- five-swing Classic cap;
- Backstab/Smash full-round scheduling and Bash doubled energy.

The recovered non-default Speed-percentage branch exists in the formula authority, but the exact native live source/value for a non-default player Speed adjustment is still evidence-gated. Normal live weapon combat therefore keeps the native default 100% branch rather than inventing a mapping.

## Other recovered combat formulas now separately scored

Executable regression assertions cover:

- Strength maximum-damage contribution;
- natural critical chance and final diminishing returns;
- critical percentile boundary and 2x..4x maximum-damage critical range/order;
- Quick & Deadly;
- Backstab accuracy;
- Bash and Smash transforms and timing.

Backstab damage endpoint reconstruction, the broad general physical-damage/DR path, class-combat accuracy ordering, non-default Speed adjustment integration, some native combat narration, and death/reward/drop ordering remain STRONG or UNKNOWN as appropriate. This pass does **not** claim complete combat parity.

## Runtime audit

New audit surface:

- in game: `audit combat`
- host CLI: `--audit-combat`

The audit reports physical/spell timing, stock hit bounds, Dodge cap, swing cap, inclusive percentile boundaries, physical-formula regression status, and remaining native boundaries.

## Score movement

Code Update 0277 baseline:

- strict: **44/113 = 38.9%**
- evidence-backed: **65/113 = 57.5%**

Code Update 0278:

- strict: **55/128 = 43.0%**
- evidence-backed: **80/128 = 62.5%**
- UNKNOWN: **48/128 = 37.5%**

The denominator grows because individual combat behaviors are now explicitly scored instead of remaining hidden inside the broad combat-native boundary.
