# Worldgroup Spell/Power Executor — Update0015

Direct disassembly closes `wccmmud.dll!0x00461273-0x0046196B` as the shared CAST/INVOKE lookup and target-dispatch wrapper.

The function accepts a player record, action selector, and input-word offset/mode. It rejects null players and fear-gated players, clears transient action flags, enforces the CAST (`0x1F`) versus INVOKE (`0x6C`) class rules, resolves a named spell or power, validates that the player may use it, parses the remaining target text, and dispatches by target mode.

The target classifier returns proven mode values `1`, `2`, `4`, and `8`. Modes `1`, `2`, and `8` call distinct downstream execution routines at `0x00444712`, `0x00447110`, and `0x00449232`. Mode `4` follows a self/no-external-target completion path. Unsupported target classes are rejected.

No direct textblock invocation is proven inside this wrapper. Textblock closure therefore moves to the three downstream target-mode dispatchers rather than being inferred from command-level diagnostics.
