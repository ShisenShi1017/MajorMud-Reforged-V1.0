# CodeUpdate0306 / Pass 35 — Classic Worldgroup RNG direct-call boundary

Pass 35 advances only the proven native randomness import boundary. It does **not** globally replace gameplay random callers and does not claim the unresolved `_genrdn`/`_lngrnd` bounded-remainder loop.

## Proven exact-version boundary

For retained `WCCMMUD.DLL` SHA-256 `5a99e7c34cf9cf58dc6e97fc3ef28f1bd83f9bcec15ca59ad08826d2f21d2077` and `WBBMMUD.DLL` SHA-256 `88e48a0bfbd9e8b7cf806c705b7be203168a2cfacaef5e35dad2aec5823ea091`:

- each module has **146** direct `_genrdn` calls through import thunk `0x00478CB2`;
- each module has **42** direct `_lngrnd` calls through import thunk `0x004789E2`;
- combined direct-call census totals are 292 and 84 respectively;
- the retained callsite artifact preserves only 64 explicit `_genrdn` addresses per module, despite the proven 146 census count; all 42 `_lngrnd` addresses are retained;
- the bounded caller-side argument analyzer likewise covers 64 `_genrdn` callsites per module and all 42 `_lngrnd` callsites per module. Neither bounded 64-row set is promoted into a complete 146-address/argument map.

The new `WorldgroupRngDirectCallsiteRegistry` makes that proven boundary executable/validation-visible while keeping unresolved integration flags false.

## Still unresolved / fail-closed

- indirect randomness call paths;
- gameplay semantic attribution of individual direct calls;
- mapping to effective Worldgroup execution-thread CW3220MT state;
- gameplay-wide draw/mutation ordering;
- fully instruction-equivalent `_genrdn`/`_lngrnd` bounded-remainder execution.

No Classic gameplay RNG caller is rewritten in this pass. No single process-global Classic RNG stream is invented. `WCCMMUD.INI` is not added; native configuration artifacts remain evidence only and Reforged keeps its own configuration.

## Parity

**97 PASS / 76 STRONG / 27 UNKNOWN = 200**

- Strict parity: **48.5%**
- Evidence-backed: **86.5%**
