# Classic Spell, Status, and Effect Closure — CodeUpdate0139

The generated Classic corpus is now exhaustively classified against decoded target/type fields, decoded public ability vectors, and active runtime consumers.

- 1,350/1,350 spells classified
- 1,344 ability-backed spell definitions
- 6 structural cleanup/control definitions with no direct effect vector
- 105/105 distinct decoded ability IDs assigned to an execution family
- 111/111 generated status-effect definitions runtime-authoritative
- 0 unclassified spells or status effects

Execution families:

- direct magnitude: decoded damage, damage-after-MR, healing, drain, cure/dispel, resource, and terminal magnitude operations
- persistent status: combat/stat/resistance/movement/casting modifiers projected into authoritative character status
- eligibility/affinity: living/undead/animal/alignment/magical and non-magical gates
- native presentation: start, description, confusion, shock, and end-cast message references
- world dispatch: summon, teleport, textblock, rune, shatter, kill-spell, scatter-item, temporary-spell, door, cast-on-end, and shadowform dispatch
- structural control: six records whose decoded ability vector is empty

The live spell catalog now retains every decoded ability vector. Damage and healing selection uses decoded ability IDs first and only falls back to the decoded target/attack classification when neither direct vector exists. Existing persistent status, expiration, resistance, resource, cure/dispel, and shared-world mutation consumers remain authoritative.

Exact semantic names and output ordering inside still-opaque native dispatch branches are not invented. They remain structurally classified by numeric ability ID and proven dispatcher family.

Verification:

- `--audit-classic-spell-effects`: PASS
- `--smoke-spells`: PASS, 1,350 catalog records
- `--smoke-spell-execution`: PASS
- Server Release build: PASS, 0 warnings/errors
