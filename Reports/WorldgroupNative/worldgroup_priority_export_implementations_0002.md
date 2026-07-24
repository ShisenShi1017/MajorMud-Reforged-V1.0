# Worldgroup Native Closure Update0002

Generated: 2026-07-16T06:39:10.035263+00:00

## Proven export implementations

### `_sameas`
- RVA: `0x0003DAE4`; ordinal: `1134`; size: `68` bytes; ABI: `cdecl`.
- Prototype: `int16/bool _sameas(const char* left, const char* right)`.
- Semantics: Returns 1 only when both NUL-terminated strings have identical length and each byte compares equal after the host character-fold helper; otherwise returns 0.
- Direct calls: 0x0044BF22 character-fold helper.
- Globals: none.

### `_genrdn`
- RVA: `0x0000738C`; ordinal: `1452`; size: `82` bytes; ABI: `cdecl`.
- Prototype: `int32 _genrdn(int32 minimum, int32 exclusiveMaximum)`.
- Semantics: If exclusiveMaximum is 0 returns 0. If exclusiveMaximum <= minimum returns minimum. Otherwise repeatedly draws from the shared host PRNG and accumulates bounded remainders until the result is at least minimum; result remains below exclusiveMaximum.
- Direct calls: 0x0044BE08 shared host PRNG.
- Globals: none.

### `_lngrnd`
- RVA: `0x000073DE`; ordinal: `1453`; size: `82` bytes; ABI: `cdecl`.
- Prototype: `int32 _lngrnd(int32 minimum, int32 exclusiveMaximum)`.
- Semantics: Instruction-equivalent range routine to _genrdn with the same edge cases and shared host PRNG dependency.
- Direct calls: 0x0044BE08 shared host PRNG.
- Globals: none.

### `_tfsopn`
- RVA: `0x0003E7DC`; ordinal: `1275`; size: `250` bytes; ABI: `cdecl`.
- Prototype: `int16 count _tfsopn(const char* filePattern)`.
- Semantics: Normalizes and bounds a file pattern into the TFS global buffer, derives its path component, enumerates matching files with _fnd1st/_fndnxt, stores the first search result, sets TFS state to 1 when a match exists or 0 otherwise, and returns the number of matches.
- Direct calls: _stzcpy, _fileparts, _fnd1st, _fndnxt, _catastro.
- Globals: 0x00463F7C pattern buffer, 0x00464084 derived path pointer, 0x00463E68 find data, 0x00463DDC TFS state.

### `_tfspfx`
- RVA: `0x0003E9D8`; ordinal: `1276`; size: `82` bytes; ABI: `cdecl`.
- Prototype: `int16/bool _tfspfx(const char* prefix)`.
- Semantics: Checks whether the supplied prefix matches the beginning of the active TFS text buffer using _sameto. On match it advances by strlen(prefix), skips whitespace, stores the suffix pointer globally, and returns 1 only when the remaining suffix is non-empty.
- Direct calls: _sameto, strlen helper, _skpwht.
- Globals: 0x00463DE0 active TFS text buffer, 0x00463E64 suffix pointer.

### `_tfsabt`
- RVA: `0x0003EA3E`; ordinal: `1271`; size: `30` bytes; ABI: `cdecl`.
- Prototype: `void/int32 zeroed side effect _tfsabt()`.
- Semantics: If TFS state equals 3, closes/releases the global TFS handle through the host close helper, then unconditionally resets TFS state to 0.
- Direct calls: 0x0044BE50 close helper.
- Globals: 0x00463DDC TFS state, 0x00463F78 TFS handle.

## Remaining boundaries
- Recover the shared PRNG implementation and seed lifecycle at `0x0044BE08`.
- Resolve the TFS find-data structure and downstream open/read/write/transaction state transitions.
- Continue callback and dispatcher tracing.
