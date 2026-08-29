# Global RNG Contract Exhaustion — PASS60 / Update0331

The broad RNG dependency is now **STRONG** at the native contract boundary.

## Exact retained behavior

- Borland/CW3220MT uint32 state transition: `seed = seed * 0x015A4E35 + 1` modulo 2^32.
- Raw result: `(seed >> 16) & 0x7FFF` (`0..32767`).
- Startup seed: `(hour << 11) + (minute << 5) + (second >> 1)` with two-second resolution.
- CRT PRNG storage: thread-local native runtime state.
- `_genrdn` / `_lngrnd`: exclusive-maximum host range boundary.
- Direct call census per retained MajorMUD module: 146 `_genrdn`, 42 `_lngrnd`.
- Retained direct addresses per module: 64 `_genrdn`, 42 `_lngrnd`.

## Remaining native dependencies

The following remain evidence-gated and are not inferred:

1. the missing 82 direct `_genrdn` addresses/prototypes per module;
2. indirect native randomness call paths;
3. semantic attribution of every randomness caller;
4. caller-to-effective Worldgroup execution-thread CRT mapping;
5. instruction-equivalent `_genrdn`/`_lngrnd` bounded-remainder execution;
6. exact gameplay-wide draw ordering across subsystem boundaries.

No gameplay RNG caller was rewritten by PASS60, and no process-global Classic stream was invented.
