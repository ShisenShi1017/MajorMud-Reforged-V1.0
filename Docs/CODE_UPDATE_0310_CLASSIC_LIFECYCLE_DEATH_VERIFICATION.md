# Code Update 0310 — Classic Lifecycle / Death Hypothesis Verification (Pass 39)

Pass 39 verifies the supplied combat-priority, LINKDEATH/logout, and death-XP pages against retained MajorMUD evidence. The pages are hypotheses only; unsupported formulas and modern server architecture are not copied into Classic.

## Result

`character.native-lifecycle-boundary`: **UNKNOWN -> STRONG**.

The independently supported MajorMUD-owned boundary is now explicit:

- EXIT/QUIT waits **5 seconds**.
- Graceful exit automatically saves the character.
- Native FOLLOW help proves that an explicitly exiting follower stops following.
- SUICIDE consumes one life like normal death; last-life SUICIDE / SUICIDE NOW rerolls. This does **not** prove the normal-death XP formula.

## Combat document correction

The supplied global priority equation `ActionDelay = WeaponSpeed - Agility/20 - HasteSkill/10` is **rejected** for stock Classic. The already-PASS native combat boundary uses attack-energy cost, 1000 energy per physical round, carry-forward remainder, and a five-swing cap. Exact cross-actor ordering for simultaneous due attacks, including whether a lethal earlier attack suppresses a later due swing, remains UNKNOWN.

## LINKDEATH / logout boundary

A fixed three-minute MajorMUD LINKDEATH timer, same-user/IP socket reattachment, player.json persistence, timer pausing during combat, highest-level leader succession, mercenary tavern return, and charm-hostility-on-exit are not promoted. Same-user/IP socket/account reattachment is Worldgroup host plumbing and is excluded from MajorMUD gameplay parity. MajorMUD-owned unexpected-disconnect consequences remain in scope and unresolved.

## Death XP boundary

`character.death-xp-loss-exact` remains **UNKNOWN**. Pass 39 does not implement or credit:

- immunity below level 10,
- `floor(total XP / 10)`,
- a 2,000,000 XP cap,
- exactly-one-level regression, or
- post-delevel HP/mana pool ordering.

Existing PASS evidence still says race/class EXP percentages construct the experience chart; they are not silently reused as a death-loss multiplier.

Parity after Pass 39: **97 PASS / 80 STRONG / 23 UNKNOWN = 200**; strict **48.5%**, evidence-backed **88.5%**.
