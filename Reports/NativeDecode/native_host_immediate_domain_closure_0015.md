# Final Native Closure 0015 — Host Immediate Argument Domains

## Proven

- Analyzed positional caller evidence for 121 imported Worldgroup host symbols.
- Recovered 72 finite immediate-only argument domains.
- 38 domains contain one observed constant and 34 contain multiple constants.
- 23 domains include a proven zero/null literal.
- 0 domains contain at least one negative source literal when interpreted as signed x86 immediate data.
- 3 domains have a conservative power-of-two/OR-combination bit shape.
- WCC/WBB parity is inherited from the validated Update0014 positional profiles.

## Runtime effect

`NativeHostImmediateDomainRegistry` exposes exact observed caller constants while enum names, flag meanings, parameter types, and host behavior remain fail-closed.

## Remaining boundary

The matching Worldgroup host binary or symbols are still required to turn these constant domains into authoritative enums, masks, and formal prototypes.
