# Worldgroup PRNG Runtime — Update0003

## Proven implementation

- `WGSERVER.EXE` imports `_rand` and `_srand` from `CW3220MT.DLL`.
- Runtime SHA-256: `4a2f6889f9dc5cfdf4ac44156dde8db0d20c6a0f63db06c576ac0bbba2011a15`.
- `_srand` stores a 32-bit seed in thread-local runtime storage at offset `0x44` and clears offset `0x48`.
- `_rand` advances the state with `seed = seed * 0x015A4E35 + 1 (mod 2^32)`.
- `_rand` returns `(seed >> 16) & 0x7FFF`, an inclusive range of 0 through 32767.
- WGSERVER has one statically reachable `_srand` call at `0x0041E5A8`.
- The seed is the zero-extended `_now` result: `(hour << 11) + (minute << 5) + (second >> 1)`.
- Startup seed resolution is two seconds.

## Runtime consequence

`_genrdn` and `_lngrnd` are deterministic for a given thread and startup seed. Their shared generator is no longer an unresolved external dependency.
