# MajorMUD Parity Scorecard — Update 0297 / Pass 26

- PASS: **73**
- STRONG: **76**
- UNKNOWN: **28**
- Total scored requirements: **177**
- Strict parity: **41.2%**
- Evidence-backed: **84.2%**

Pass 26 removes the generic `native.world-containers` bucket from Classic scoring. Native death piles, portable item ownership, banks, gang-house vaults, and treasure-chest loot remain owned by their actual subsystems.

Doors remain STRONG: explicit Picklocks/Strength requirements are now consumed correctly, including Boolean Key-exit Strength alternatives, but the final native PICKLOCK/BASH success dispatcher and exact messages remain unresolved. Traps remain UNKNOWN because per-exit native Para2 activation state is not available in the retained projection.
