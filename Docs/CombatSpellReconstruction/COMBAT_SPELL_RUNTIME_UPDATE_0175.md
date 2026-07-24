# Combat & Spells Runtime Update 0175

Implemented an evidence-gated, deterministic death-event fan-out boundary. Lethal HP must already be clamped to zero. Every promoted event sink prepares before any sink commits; a later failure rolls committed sinks back in reverse order. Combat owns cancellation, status/threat/target cleanup ordering, participant defeat, and request emission. Character, NPC, Quest, Economy, loot, corpse, respawn, and resurrection systems retain ownership of their mutations. Missing sinks and unresolved policies fail closed.
