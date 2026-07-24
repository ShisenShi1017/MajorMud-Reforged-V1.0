# Combat & Spells Runtime Update 0178

Implemented an evidence-gated transactional runtime for teleportation and movement spells. The coordinator validates canonical identities, destination availability, room restrictions, combat restrictions, consent, same-room and cross-world policy before any state mutation. It then prepares and commits action interruption, threat cleanup, combat cleanup, status disposition, portal creation, primary/secondary movement, event emission and optional persistence in a declared deterministic order. Partial commits roll back in reverse order.

Supported request kinds are self teleport, target teleport, party teleport, recall, portal creation, summon-target movement, forced movement, knockback, pull and swap. Production destination selection, distances, portal lifetime, consent exceptions and spell.1304 semantics remain fail-closed.
