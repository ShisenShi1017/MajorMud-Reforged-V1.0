# NPC Lifecycle Combat State Patch

Validation0009 converts a small simulator placeholder into explicit lifecycle state for future server validation planning.

Expected-model flow:

1. Spawn.
2. Idle.
3. Player-started combat engagement.
4. Defeat.
5. Reward projection.
6. Combat state cleared.
7. Death-triggered respawn due minute.
8. Respawn when the due minute is reached.

The simulator remains an expected-model tool. It must not be treated as proof that original MajorMUD timing, AI, formulas, or live event ordering have been certified.

## Next Validation Candidate

Validation0010 should examine whether canonical data contains enough evidence to model autonomous NPC aggro and assist boundaries. If evidence is insufficient, the correct result is a documented blocker rather than inferred behavior.
