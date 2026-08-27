# Code Update 0283 — Character Progression Native Closure (Pass 13)

## Scope

Pass 13 closes retained-stock formula gaps that remained after the Character/Advancement pass. It does not guess unresolved native lifecycle, death-XP-loss, or cross-category modifier ordering.

## Stock XP thresholds

The current retained MMUD Explorer stock branch exposes `CalcExpNeeded_STOCK` and `GetExpModifiers_STOCK`. Reforged now uses that recovered schedule directly for Classic progression:

- race/class chart = `(class ExpTable + 100) + race ExpTable`
- level 1 = `0`
- level 2 = `chart * 10`
- level-specific multiplier/divisor schedule is preserved through level 59+
- recovered unsigned-32-bit rollover and billions-tabulation behavior is preserved
- generated progression rows may still override by specificity only when they agree with the recovered stock formula; conflicts fail closed
- absence of generated progression rows no longer blocks the recovered stock schedule

The formula is STRONG rather than PASS because the retained implementation is not the original MajorMUD executable callsite.

## Character-point awards

Stock `CalcCPLevel` preserves the promotion schedule:

`award(targetLevel) = (((targetLevel - 1) div 10) * 5) + 10`

Examples: level 2 awards 10 CP, level 11 awards 15 CP, and level 21 awards 20 CP. TRAIN now uses this schedule and persists the award.

## Picklocks

The previous Picklocks conflict is resolved as a branch-ownership issue:

- the Charm-bearing formula is inside MMUD Explorer's GreaterMUD branch
- the stock branch preserves the original Pascal formula using Level, Agility, and Intellect
- ability 37 grants Picklocks in race/class definitions
- ability 180 modifies PickLocksValue
- Reforged now uses the stock base formula for native-created characters and composes acquired, equipment, and status bonuses without counting the projected acquired bonus twice
- imported legacy characters that contain only a literal Picklocks skill retain that imported value for compatibility

Picklocks is promoted from UNKNOWN to STRONG.

## Lives and death integration

The runtime now consistently enforces the already-scored Classic life rules:

- new characters begin with 9 lives
- normal death removes exactly one life
- level-up restores exactly one life, capped at 9
- zero lives prevents respawn
- MajorMUD Plus life purchase cannot exceed the Classic 9-life cap
- Outlaw-or-worse respawn resolves to decoded map 1 room 142, **Earthen Tomb**
- non-Outlaw respawn resolves to decoded map 1 room 2189, **Temple, Halls of the Dead**

The exact death experience-loss formula remains UNKNOWN.

## Parity score

Update 0283 scores **69 PASS / 56 STRONG / 48 UNKNOWN = 173 requirements**.

- strict proven parity: **39.9%**
- evidence-backed parity: **72.3%**
- UNKNOWN count: **50 → 48**

The denominator increased by one because the old combined XP/CP requirement was decomposed into two independently scored rules.

## Validation

`Tools/Validation/validate_pass13_native_character_progression_closure.py` verifies recovered XP vectors including rollover behavior, CP boundaries, Picklocks level-15/16/17 behavior, ability semantics, decoded respawn rooms, life wiring, ledger states, and historical Pass 10–12 unified-ledger pointers.

The .NET SDK is unavailable in the current analysis environment, so Windows `dotnet build` remains the final compiler/runtime confirmation.
