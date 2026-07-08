# Runtime-0014 — Persistence / Save-Load

This milestone adds Reforged runtime persistence for offline saves and future server integration.

## Included

- `Runtime/persistence/persistence_runtime.py`
- Atomic JSON save store
- Versioned save envelopes
- SHA-256 checksum verification
- Character save/load
- World save/load
- Runtime snapshot save
- Ability and quest persistence blocks
- Inventory/equipment/state round-trip validation

## Accuracy note

This is **not** a claim to match classic MajorMUD binary user records. It persists Reforged runtime state using a documented JSON schema so the runtime can move toward server save/load safely.

## Persisted character state

- runtime ID/name/type
- room reference
- HP/Mana/CP
- level
- stats/skills
- inventory/equipment
- flags including class/race IDs
- ability grants block
- quest progress block

## Persisted world state

- world ID and tick
- players, monsters, NPCs
- ground items
- corpses
- events
- spawn timers
- combat pairs
- status effects
- world flags

## Remaining work

- Account/login records
- Multi-character account vaults
- Server-side autosave loop
- Save migrations beyond v0014
- Classic binary import/export research
