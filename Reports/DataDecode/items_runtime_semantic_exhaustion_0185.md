# Items Runtime Semantic Exhaustion — CodeUpdate0185

## Executable advance

CodeUpdate0185 adds evidence-gated NPC death-to-corpse item transfer. Eligible top-level NPC-owned inventory and equipment roots move into a persistent corpse while retaining runtime identity and complete portable-container subtrees.

## Ownership boundary

Items owns item identity, corpse placement, recursive ownership clearing, hierarchy preservation, validation, persistence mutation, and rollback. NPC Runtime owns death sequencing, loot-table selection, room selection, corpse timing, and output messages.

## Fail-safe behavior

The runtime rejects missing NPC or room identities and invalid exclusive-loot windows. Only explicitly projected `npc_no_drop` or `no_loot` metadata retains items; unknown Classic flag bits are not guessed.

## Native dependencies

Classic NPC drop flags, equipped-drop rules, guaranteed/random/rare loot tables, corpse timing, limited-item suppression, and exact messages remain deferred to the final WCC/WBB/native DLL exhaustion pass.
