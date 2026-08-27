# Combat XP Sharing Correction 0294

Validation target: the Pass 23 cumulative runtime plus the XP-sharing correction requested after Pass 23.

Required invariants:

- NPC XP recipients are exact-NPC active combatants at death, not party members.
- Integer quotient is awarded equally.
- 525 / 2 yields 262 each with remainder 1.
- 125 / 2 yields 62 each with remainder 1.
- Highest damage and killing blow do not receive the remainder based on recovered runtime evidence.
- The nonzero remainder remains evidence-gated and unallocated.
- Broader party coordination remains UNKNOWN.
