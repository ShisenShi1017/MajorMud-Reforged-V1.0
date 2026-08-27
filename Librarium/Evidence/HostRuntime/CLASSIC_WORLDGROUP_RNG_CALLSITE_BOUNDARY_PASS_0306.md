# Pass 35 evidence note — Classic Worldgroup RNG direct-call boundary

Classification: **PROVEN, direct-call boundary only**.

Retained native callsite evidence pins the exact WCCMMUD/WBBMMUD module versions and direct calls to WGSERVER import thunks. Both module variants expose the same direct randomness-call census: 146 `_genrdn` calls at thunk `0x00478CB2` and 42 `_lngrnd` calls at thunk `0x004789E2` per module. The retained artifact carries only 64 explicit `_genrdn` addresses per module while preserving all 42 `_lngrnd` addresses; the missing `_genrdn` addresses are not reconstructed. The direct-call analyzer explicitly excludes indirect register/data calls.

The bounded argument-position evidence observes 64 `_genrdn` callers per module and 42 `_lngrnd` callers per module. That narrower `_genrdn` coverage is retained as a limitation, not silently expanded.

Pass 35 therefore records and validates the exact direct boundary without changing gameplay draw behavior. Thread-state ownership mapping, indirect paths, callsite semantics, draw ordering, and the instruction-equivalent host range loop remain evidence-gated.
