# Items Runtime Semantic Exhaustion — CodeUpdate0201

## Executable advance

Added coordinated save-generation stamping and validation across Items, Character, Economy, Quests, Events, NPC, and World snapshots. Integrity-backed recovery is accepted only when the recovered Items generation matches every authoritative subsystem generation.

## Safety boundary

Missing or mismatched generations fail closed. No external subsystem state is altered and no generation is inferred.

## Native boundary status

Not reached. Global limited-item reconciliation, administration, and the final exhaustion audit remain non-native work. Exact native journal/checkpoint behavior remains reserved for the final native pass.
