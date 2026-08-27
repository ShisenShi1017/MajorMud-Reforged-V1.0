# Pass 25 Validation — Update 0296

Validation target: Doors / Locks / Traps / Room Effects Closure.

## Current checks

- Dedicated Pass 25 static validation: **59/59 PASS**.
- Combat runtime validator: **PASS**.
- Combat runtime Part 2: **PASS**.
- XP-sharing correction validator: **16/16 PASS**.
- Native economy markup validator: **PASS**.
- Gameplay Group 4 validator: **PASS**.
- Changed C# structural checks: **PASS**.
- Current parity ledger: **73 PASS / 76 STRONG / 29 UNKNOWN = 178**.

The validator confirms that traversal no longer auto-applies Picklocks/Strength thresholds; PICKLOCK and DISARM are explicit living-world commands; BASH checks a decoded door direction before combat fallback; exact door and trap rolls remain evidence-gated; and the generic environmental-hazard shell is excluded from Classic scoring.

A .NET compiler is not installed in this execution environment, so build validation remains pending until the package is installed in a .NET-capable environment.
