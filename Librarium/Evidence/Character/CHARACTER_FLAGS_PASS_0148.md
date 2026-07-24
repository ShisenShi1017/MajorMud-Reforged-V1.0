# Character Flags Pass 0148

## Promoted behavior

Two existing character-condition flags have sufficient runtime evidence for typed promotion.

- `dead` is character-owned, persisted on player defeat, and explicitly cleared by respawn.
- `resting` is character-owned and persisted in the character condition store; its status-line read behavior is proven, but its setter and reset conditions are not yet recovered.

`RuntimeCharacterFlagResolver` is now the only promoted mutation path. It synchronizes the legacy condition dictionary with the semantic flag ledger, records scope/lifetime/evidence/source, advances mutated records to save version 4, and rejects unsupported flag IDs.

## Compatibility

Save versions 1 through 3 remain readable. Older records are not rewritten merely by loading. The semantic ledger is populated when a promoted flag is next changed or normalized.

## Blocked evidence

No meanings are assigned to unknown native character bits. Session, combat, quest, area, world, and native bitfield flags remain unresolved unless separately proven. The `resting` setter and clear/reset rules also remain blocked.
