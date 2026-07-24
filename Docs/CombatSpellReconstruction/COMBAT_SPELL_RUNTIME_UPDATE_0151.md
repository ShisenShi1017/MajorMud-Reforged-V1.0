# Combat & Spell Runtime Update 0151

Update 0151 binds canonical generated weapon and spell definitions to the shared deterministic resolution engine and adds an atomic mutation boundary.

## Executable advance

- `RuntimeCombatSpellBindingCatalog` requires canonical `item.` or `spell.` IDs, evidence, valid magnitude ranges, and safe fallbacks for strongly correlated bindings.
- `RuntimeCombatSpellBoundResolver` rejects unbound definitions, unavailable profiles, and profile/binding range or damage-type mismatches before randomness or mutation.
- `RuntimeCombatSpellAtomicExecutor` resolves completely before mutation, captures the target state, commits once, and restores the snapshot if the sink throws.
- `item.1622` is bound using its promoted WCC-backed 14–33 weapon damage range.
- `spell.1304` preserves extracted 10–50 magnitude plus opaque attack/resistance enum identities without assigning unsupported semantic names.

## Ownership

The resolver owns validation and trace construction. The atomic executor owns transaction ordering. Character/NPC or spell-effect sinks retain ownership of HP, resources, persistence, death, and world-state mutation.

## Validation

The validator checks cumulative preservation, canonical IDs against `Data/Classic`, exact promoted source-field agreement, evidence/fallback requirements, C# structural references, generated index consistency, manifest hashes, and ZIP integrity. Smoke vectors verify canonical binding, bounded promoted magnitude, single commit, rollback, and fail-closed unbound behavior.

## Remaining dependency

Exact hit, critical, resistance, saving-throw, targeting, spell cost timing, and interruption semantics remain unpromoted until evidence supports each path.
