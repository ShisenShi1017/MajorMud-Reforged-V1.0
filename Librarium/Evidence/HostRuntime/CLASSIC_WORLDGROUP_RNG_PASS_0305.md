# Classic Worldgroup RNG closure — Pass 34 / Update 0305

## Result

The MSVC LCG candidate supplied for validation is **rejected** for the bound Worldgroup runtime. The retained native closure identifies the Borland/CW3220MT runtime contract instead.

## Proven raw PRNG contract

Bound runtime: `CW3220MT.DLL`, SHA-256 `4a2f6889f9dc5cfdf4ac44156dde8db0d20c6a0f63db06c576ac0bbba2011a15`.

- `CW3220MT!_srand` RVA `0x00016100`: stores the supplied 32-bit seed in thread-local CRT state.
- `CW3220MT!_rand` RVA `0x0001611C`:
  - `seed = seed * 0x015A4E35 + 1` modulo `2^32`;
  - return `(seed >> 16) & 0x7FFF`;
  - output domain is `0..32767`.
- `WGSERVER` `_rand` import thunk RVA `0x0004AE08` dispatches to that CRT export.

The multiplier is decimal **22695477**. Increment is **1**. This differs from the proposed MSVC constants `214013` and `2531011`.

Exact vector from seed `1234`:

- Borland/Worldgroup next seed: `2236414843`
- returned value: `1356`
- proposed MSVC next seed: `266623053`
- proposed MSVC returned value: `4068`

The retained native formula is also independently corroborated by MBBSEmu test vectors recorded as verified against Borland C++ 4.5 for DOS. That corroboration is secondary; the bound native disassembly remains authoritative.

## Proven startup seed lifecycle

- `WGSERVER` startup seed call RVA `0x0001D5A4` calls exported `_now`, zero-extends the result, and passes it to `_srand` once in the statically reachable startup path.
- `WGSERVER!_now` RVA `0x000360FA` packs local clock fields as:
  - `(hour << 11) + (minute << 5) + (second >> 1)`
- This is a 16-bit packed time seed with two-second resolution.

Examples: `00:00:00` and `00:00:01` both seed `0`; `00:00:02` seeds `1`; `23:59:58` and `23:59:59` both seed `49021`.

## Proven host range contract

The retained WGSERVER export closure marks `_genrdn` (RVA `0x0000738C`) and `_lngrnd` (RVA `0x000073DE`) as PROVEN. Their boundary contract uses an **exclusive maximum**, returns the minimum when the maximum is not greater than the minimum, and consumes the shared host PRNG on ordinary range paths. `_lngrnd` is instruction-equivalent to `_genrdn` for this range contract.

## What remains UNKNOWN

Pass 34 does not invent a single process-global stream. The CRT seed storage is proven thread-local. What remains unresolved for MajorMUD parity is the exact mapping of all MajorMUD gameplay callsites onto effective Worldgroup execution-thread PRNG state, including exact draw/mutation ordering across direct and indirect gameplay paths, and a fully instruction-equivalent Reforged port of `_genrdn`/`_lngrnd`'s bounded-remainder loop.

The existing `RuntimeGlobalRandomService` xorshift/multi-stream implementation is retained as **Reforged extension infrastructure** and is not claimed as the Classic native RNG.
