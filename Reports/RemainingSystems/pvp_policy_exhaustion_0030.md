# PvP Policy Exhaustion — Update 0326

PASS55 promotes the broad `native.pvp-policy` assertion from **UNKNOWN** to **STRONG** by closing the native owner boundary and reconciling it with retained exact native help. This is not a claim that every target-admission, kill, corpse, guard, cooldown, or message branch is exact.

## Native owner surface

Both retained MajorMUD distributions expose the same selected PvP owner anchors. Dedicated functions include `_attack_user_user`, `_check_kill_user`, `_is_combatting_user`, `_is_in_pvp_combat`, attack command/target dispatch, robbery, and Evil-Point coupling. This proves that user-vs-user policy/execution is MajorMUD-owned and locates its native dependency groups. Export identity does not prove internal branch ordering.

## Exact retained policy help

Native `HELP LAWS` (HL1B11) states that attacking innocents and stealing are the two broad criminal acts; the town does not care if a hated person is slain; attacking a Lawful player gives three times the Evil Points; and Seedy-or-worse consequences are described by reputation. The retained `GOODTEXT`/`GOODPMPT` creation text is stronger than a generic consent toggle: Lawful is an explicit one-way character choice, Lawful characters may not instigate actions that would give Evil Points, attackers/robbers of Lawful receive triple Evil Points, Lawful may still attack bad-reputation targets, and the title cannot be removed except by creating a new character. Setup data retains `Enable the choice of Lawful? YES`.

The setup default `Maximum level difference for PVP? 100` remains exact. Native practice-arena help (HL1B55) states no Evil Points, no life loss, and Halls Of The Dead revival with HP captured at arena entry. Native FORGIVE help says an accidental victim can counter the attacker's Evil Points for approximately five minutes; PASS55 deliberately does not convert “approximately” into an exact timer.

## Reforged generic PvP policy boundary

`RuntimePvpPolicyService` remains useful extension infrastructure, but `ConsentOnly`, generic Open/Forbidden zones, same-party/guild denial, synthetic per-target cooldowns, logout restrictions, and `RuntimePvpKillRequest` routing receive **zero Classic parity credit**. The Classic boundary is now explicit in `RuntimeClassicPvpPolicyAuthority`.

## Still native-dependent

Exact realm enable persistence/default semantics, Lawful creation-record encoding, native safe/restricted zone flags, target-admission branch order, any party/gang restrictions, player kill credit/death penalty ordering, corpse access, guard/bounty composition, cooldown/logout behavior, exact PvP messages, and native attack/kill branch ordering remain fail-closed. These prevent PASS.
