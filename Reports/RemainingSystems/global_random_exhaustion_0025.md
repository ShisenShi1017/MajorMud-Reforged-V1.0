# Global Random Service Exhaustion — Update0025

Implemented a deterministic Reforged random-stream service with world, session, and action scopes; inclusive checked ranges; rejection sampling; state snapshots; restore/replay hooks; and fail-closed validation.

The xorshift128+ compatibility engine is **not** asserted to be MajorMUD's native algorithm. Exact algorithm, seeds, stream sharing, save behavior, and endpoint semantics remain assigned to the final WCC/WBB/native pass.
