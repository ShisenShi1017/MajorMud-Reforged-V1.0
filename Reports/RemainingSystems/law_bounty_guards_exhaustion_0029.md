# Law, Bounty, and Guards Exhaustion — Update 0325

PASS54 closes the broad native law/EP/guard-jail ownership boundary from **UNKNOWN** to **STRONG**. It does not claim full arrest/fine/confiscation/bounty parity.

## Native ownership recovered

WCCMMUD and WBBMMUD each expose the same 17 selected law/Evil-Point owner anchors. They cover EP award/admission and room-warning hooks, robbery-law coupling, timer/forgiveness/clearing, player display/commands, and explicit saved-EP get/save ownership. This is a real MajorMUD-native subsystem surface; the generic Reforged law-policy service is not used as Classic evidence.

## Guard jail graph recovered

Four decoded law-enforcement NPCs — guardsman 13, guardsman 14, Sheriff Lionheart 40, and elite guardsman 757 — use hit spell 583 `jail`. Spell 583 invokes TextBlock 1509, whose exact decoded script is `testskill current_hp 0 9624:price 0`. TextBlock 9624 then selects one of five jail-time effects by retained alignment/EP conditions:

- EP 210+ -> spell 586, 200 seconds;
- EP 120..209 -> spell 641, 175 seconds;
- EP 80..119 -> spell 642, 150 seconds;
- EP 40..79 -> spell 643, 125 seconds;
- final `goodaligned 39` branch -> spell 644, 100 seconds.

Each row also casts jail teleport spell 584. Its decoded record targets map 1 and stores room range 541..545. The exit-control chain is spell 585 -> TextBlock 9113 (`roomitem 1363:cast 929`) -> spell 929, whose decoded fixed teleport is map 1 room 1076. The data graph is retained exactly; exact combat/death ordering, TextBlock comparator semantics, room-range draw ordering, and rendering are not inferred.

## Already recovered

The project already has PASS Evil-Point reputation bands and default forgiveness configuration/arithmetic, plus STRONG native-help enforcement for Lawful 3x penalties, Outlaw/Criminal guard behavior, Villain divine-intervention wording, and the 300+ attack lockout. PASS54 connects those facts to the broader native owner surface instead of inventing a second legal model.

## Still native-dependent

Exact per-action crime/witness classification, guard alert/spawn cadence, Villain/Fiend extra actor/cadence, fine/confiscation/bounty arithmetic, citizen reporting, FORGIVE scheduling/history, law/jail messages, persisted EP record encoding, and the exact jail execution/RNG ordering remain fail-closed. These prevent PASS.
