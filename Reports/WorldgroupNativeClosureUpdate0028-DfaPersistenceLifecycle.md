# Worldgroup Native Closure Update0028 — DFA Persistence Lifecycle

## Proven boundary

MajorMUD persistence is organized around a retained Worldgroup DFA context. Callers select a block, acquire a lock, query or position the record cursor, execute an explicit mutation operation, and reset/release the active block state.

The imported mutation surface is explicit: `_dfaInsertV`, `_dfaInsertDup`, `_dfaUpdateV`, `_dfaUpdateDup`, and `_dfaDelete`. `_dfaRstBlk` is therefore a context/reset operation, not itself the write commit.

The dominant `_dfaAcqLock` caller shape contains five stack positions. Its fourth position carries lock-mode constants 5, 8, 9, 10, 12, and 13. Mode 9 is independently proven for textblock record acquisition; the remaining mode names are intentionally left unresolved.

No separate MajorMUD-imported DFA symbol named or behaving as an explicit commit, rollback, journal, checksum, or backup operation appears in the recovered import family. Such behavior must be traced inside WGSERVER or its lower storage dependencies rather than inferred at MajorMUD callsites.

## Lifecycle ordering

1. Select/open the DFA source or active block.
2. Acquire positional or absolute lock ownership.
3. Query, count, or step the retained cursor.
4. Insert, update, or delete through an explicit mutation import.
5. Reset the active block and release retained operation state.
6. Close the source when its owner is finished.

## Remaining boundary

The internal disk-write algorithm, physical flush point, rollback behavior, journal format, backup rotation, checksum calculation, migration, and crash recovery are not proven by this pass.
