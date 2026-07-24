# Combat & Spells Runtime Update 0164

Implemented an evidence-gated combat threat ledger. Physical damage, spell damage, healing, buffs, debuffs, taunts, summons, crowd control, dispels, and resurrection can produce deterministic per-observer NPC threat mutations only when a promoted policy exists. Multi-observer mutations are atomic and roll back on failure. Threat inspection is stable and auditable, but target selection remains owned by NPC AI.

Death, defeat, flee, room exit, disconnect, removal, and combat termination now have an exactly-once cleanup boundary for matching threat entries.

## Native boundary

Exact production multipliers, caps, decay, transfer, inheritance, group propagation, and persistence remain unresolved. No native values were approximated.
