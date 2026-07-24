# Combat & Spells Runtime Update 0163

Implemented deterministic death and target invalidation for the unified combat action queue. Execution owners register canonical actor and target dependencies. Promoted death, defeat, flee, disconnect, removal, or target-invalid events cancel matching queued or claimed physical attacks, spell completions, and periodic effects in the same deterministic ordering used by the queue. Duplicate invalidations and weak-hypothesis policies fail closed.

## Native boundary

Exact native precedence when death is produced during an already executing same-tick mutation remains unresolved and is retained for the final WCC/WBB/native pass.
