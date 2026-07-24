# Combat & Spell Runtime Update 0161

Implemented evidence-gated atomic attack resource and equipment transactions. Ammunition, charges, and durability can be consumed at promoted start, on-hit, or completion boundaries. Every selected resource is prevalidated, snapshotted, committed with the attack effect, and restored in reverse order on failure. Zero durability can emit policy-controlled breakage. Unresolved production timing remains fail-closed.
