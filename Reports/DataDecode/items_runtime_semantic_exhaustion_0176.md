# Items Runtime Semantic Exhaustion — CodeUpdate0176

## Executable advancement

- Added a persisted limited-item reservation owner to corpse state.
- Counted root and nested corpse-held items toward projected owner quantity limits.
- Rejected duplicate acquisition while an active corpse retains a limited item.
- Excluded the transferred corpse quantity during loot so legitimate recovery remains possible.
- Released reservations through recursive corpse decay.
- Preserved backward compatibility by resolving legacy corpse records through CharacterId.
- Added deterministic smoke and save-state validation coverage.

## Evidence boundary

Only explicit `maximum_owned_quantity` metadata activates this behavior. The runtime does not infer Classic unique/limited flags, account or world scopes, resurrection exceptions, or crash-recovery policy. Those remain native dependencies.

## Status

Owner-scoped limited-item enforcement now spans active corpses and nested portable containers. Exact Classic scope and flag mapping remain unresolved.
