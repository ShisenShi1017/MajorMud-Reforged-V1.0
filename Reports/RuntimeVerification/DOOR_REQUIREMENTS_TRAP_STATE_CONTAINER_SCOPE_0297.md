# Door Requirements / Trap State / Container Scope — Evidence Summary 0297

Pass 26 combines operator-confirmed door behavior with retained 1.11p room data and reverse-engineered editor/export metadata.

## Door requirement evidence

- Door/Gate Para2 is associated with PICKLOCK/BASH difficulty/chance rather than ordinary traversal.
- MMUD/Nightmare-derived export data renders negative Para2 values as numeric `picklocks/strength` requirements and positive nonzero values as `any`.
- The retained semantic corpus projects 1,178 numeric Door Picklocks/Strength requirements and 109 any-value Door requirements.
- Key exits separate Picklocks difficulty from a Boolean Strength alternative; 83 retained Key exits project both numeric Picklocks and Strength paths.
- PICKLOCK is owned by the character Picklocks value; BASH is owned by Strength when the exit permits that route.

The requirement boundary is strong evidence. The exact native random/success dispatcher after admission remains UNKNOWN and is not synthesized.

## Trap state evidence

The five-state trap family is recovered, as are all 295 exit-trap identities/effect operands. The current retained semantic projection did not preserve the per-record Para2 state. A stock 1.11p MDB has been located publicly, but binary extraction is unavailable in the current tool environment. Per-exit activation state therefore remains UNKNOWN and fail-closed.

## Container ownership evidence

The generic Reforged world-container service has no independently recovered Classic RoomType/command owner. Native death piles, bank storage, gang-house vault behavior, carried/nested items, and named chest loot remain with their actual subsystems. The generic `native.world-containers` UNKNOWN is removed so the same native behavior is not scored twice.
