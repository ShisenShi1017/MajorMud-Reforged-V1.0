# Worldgroup Entity/Alias Matcher — Update0013

Direct disassembly closes `wccmmud.dll!0x0046ABB4` as a bounded, player-scoped entity/alias matcher. It is not the general command-table lookup.

The function resolves the current player, clamps the candidate count at player offset `+0x478` to 100, scans 16-bit candidate IDs beginning at `+0x47A`, resolves each candidate, and compares the mutable input against entity text at offsets `+0xFA` and `+0x02`. An optional caller flag enables the fallback matcher. The routine tracks caller-owned match state and returns the selected entity record pointer or null.

The active gameplay gateway calls it at `0x00450876`. A successful result is then combined with a record resolved from player field `+0x92` and passed toward `0x00461273`, which is the next concrete action-execution boundary.
