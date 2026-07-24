# Worldgroup Native Closure Update0020

Direct disassembly closes the structural contract of `wccmmud.dll!0x00470209`. The routine is a MajorMUD-owned, colon-chained special-command interpreter taking a Worldgroup session index and mutable command line. It resolves the player, clears a 100-entry scratch reference table, temporarily NUL-terminates each colon-delimited segment, and walks an ordered keyword chain using `WGSERVER!_sameto`. Token movement is delegated to `WGSERVER!_skpwrd` and `WGSERVER!_skpwht`. The overwritten colon is restored on exit, and a non-empty remainder is processed as the next segment.

The result byte has three observed control domains: `0` continues or reports no terminal action, `1` stops the containing textblock as handled, and `2` marks rejection/defer or malformed branches. Nested helper paths preserve these controls rather than collapsing them to boolean.

The textblock fetch helper at `0x0043379C` is also closed. It writes the requested textblock ID at record offset `+0x10` and a 16-bit selector at `+0x00`, brackets the host operation with `WGSERVER!_dfaSetBlk` and `WGSERVER!_dfaRstBlk`, and calls `WGSERVER!_dfaAcqLock` with mode `9`. After local normalization at `0x0044E376`, it clears byte `+0x7E7`. It returns the signed host result only if the record ID still equals the requested ID; otherwise it returns zero.
