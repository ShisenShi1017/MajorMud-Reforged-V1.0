# Combat & Spells Runtime Update 0172

CodeUpdate0172 connects the promoted active-status modifier trace from CodeUpdate0171 to four executable boundaries: physical attacks, spell casting, combat turn ordering, and combat movement.

Blocked actions are rejected before downstream state mutation. Physical attack inputs are replaced with status-adjusted attack and defense values. Cast restrictions are checked before cast creation or resource reservation. Due turns are deterministically reordered with adjusted initiative and speed. Movement restrictions are checked before action invalidation, threat cleanup, participant transition, or room-exit handoff.

This pass does not infer production status meanings, native multiplicative ordering, caps, hidden-state removal messages, fear ownership, or charm ownership.
