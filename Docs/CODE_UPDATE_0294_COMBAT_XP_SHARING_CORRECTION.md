# Code Update 0294 — Classic NPC Combat XP Sharing Correction

## Result

The live Classic NPC-death path no longer awards the entire monster XP value to the session that delivered the killing blow. XP recipients are the persistent characters whose live combat encounter targets the exact defeated NPC instance at the moment of death. Party membership is not consulted.

For `N` eligible combatants and monster XP `X`, each observed recipient receives the integer quotient `X / N`. The retained `npc.493` ju-ju zombie is 525 XP; a native-era two-combatant walkthrough shows the follower receiving 262 XP even though that follower dealt most of the recorded damage and landed the fatal blow. This disproves both “highest damage gets the remainder” and “killer gets the remainder.”

The exact destination of the leftover `X % N` XP remains UNKNOWN. Reforged therefore does not give it to highest damage, killer, alphabetical order, or party order. It records the unresolved remainder in audit conditions and leaves it unallocated until native evidence identifies the recipient/disposition.

Race/class `EXP` percentages are treated as progression-cost modifiers rather than multipliers on monster XP earned. The active-combat quotient is awarded directly.

## Runtime changes

- Snapshot exact-NPC combat participants before combat cleanup.
- Award equal integer quotient to every eligible combatant.
- Remove party membership from Classic NPC XP eligibility.
- Remove killer-only full-XP behavior.
- End the defeated NPC encounter for every participant.
- Preserve the unresolved remainder explicitly.
- Do not promote the broader party-coordination requirement; loot and other party edges remain unresolved.

## Evidence status

- Active-combat eligibility: **STRONG** (user-supplied native behavior plus native-era runtime correlation).
- Equal integer quotient: **STRONG**.
- Highest-damage remainder: **DISPROVEN**.
- Killing-blow remainder: **DISPROVEN**.
- Exact nonzero remainder recipient/disposition: **UNKNOWN**.
