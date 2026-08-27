# Code Update 0305 — Classic Worldgroup PRNG closure

Pass 34 converts retained native Worldgroup RNG evidence into an executable Classic host-PRNG authority and rejects the proposed MSVC LCG candidate.

## Exact raw generator — PASS

The bound `CW3220MT.DLL!_rand` contract is:

`seed = seed * 0x015A4E35 + 1 (mod 2^32)`

`value = (seed >> 16) & 0x7FFF`

The state is a 32-bit CRT seed; the returned value is `0..32767`. `RuntimeClassicWorldgroupPrng` executes that exact state transition. Seed `1234` produces seed `2236414843` and value `1356`.

The screenshot's MSVC constants (`214013`, `2531011`) are therefore rejected for this Worldgroup runtime.

## Startup seed — PASS

WGSERVER seeds the CRT once on its statically reachable startup path using `_now`. `_now` packs the time as `(hour << 11) + (minute << 5) + (second >> 1)`, giving two-second resolution. `WorldgroupPrngRuntimeRegistry.PackStartupTimeSeed` now executes that exact packed-time contract.

## Host range boundary — PASS-narrow

Native `_genrdn` and `_lngrnd` use an exclusive maximum, return the minimum when the maximum is not greater than the minimum, and consume the shared host PRNG on ordinary paths. These facts are retained as a proven executable contract boundary.

Pass 34 does **not** guess an instruction-equivalent C# translation of the complete bounded-remainder loop or globally replace every existing `NextInclusive` caller. The current high-level Classic callers were written around inclusive adapters; silently swapping their endpoint convention would introduce new parity errors.

## Stream ownership correction

The old `RuntimeGlobalRandomService` xorshift128+ multi-stream design remains useful Reforged extension infrastructure, but it is not the Classic native RNG. Native CRT seed storage is thread-local. A single process-global Classic stream is therefore not asserted without a complete MajorMUD/Worldgroup execution-thread callsite mapping.

## Remaining UNKNOWN

The broad global-RNG UNKNOWN is narrowed to exact MajorMUD gameplay draw ordering, direct/indirect callsite-to-thread-state mapping, and fully instruction-equivalent `_genrdn`/`_lngrnd` range-loop activation.

## Score

Before Pass 34: **93 PASS / 76 STRONG / 27 UNKNOWN = 196**.

After Pass 34: **96 PASS / 76 STRONG / 27 UNKNOWN = 199**.

- strict parity: **48.2%**
- evidence-backed: **86.4%**
