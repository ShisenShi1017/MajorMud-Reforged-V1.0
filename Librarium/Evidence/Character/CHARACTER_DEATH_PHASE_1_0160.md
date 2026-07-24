# Character Death Phase 1 — CodeUpdate0160

## Promoted runtime contract

Player defeat and respawn now execute through one Character-owned transition resolver classified **STRONGLY CORRELATED**. The promotion combines already active runtime paths with the proven persistent `dead` flag, corpse transfer engine, retained-on-death curse behavior, and checked gold escrow.

Defeat rejects duplicate execution, validates carried gold, creates one corpse in the current room, transfers non-retained root items and nested contents, retains ability-83 roots as character-owned and unequipped, escrows all carried gold, sets HP to zero, sets the persistent dead flag, and records deterministic provenance markers before the character save.

Respawn requires both the dead flag and zero HP, requires a prior death event/corpse marker, restores maximum HP at the generated character start room, clears the dead flag, and preserves the corpse for later recovery. Corpse recovery is blocked while dead.

## Persistence and ordering

The existing Character semantic `RuntimeMarkers` ledger stores death event ID, corpse ID, death room, UTC occurrence time, gold escrow, dropped/retained root counts, classification, respawn UTC time, and respawn room. No save schema version change is required.

## Fail-closed behavior

A repeated defeat, dead/HP inconsistency, missing required death markers, invalid evidence marker, or respawn without a prior corpse transition is rejected before persistence.

## Remaining unknowns

Native XP loss, partial gold loss, corpse decay duration, resurrection penalties/order, status cleanup, alternate respawn destinations, and corpse looting permissions remain `UNKNOWN` for later evidence or the final native pass.
