# Code Update 0303 — Classic SET Profile / Communication Settings

Pass 32 promotes the exact setting forms retained in native WCCMMHLP help and corrects runtime mismatches without inventing native defaults, persistence, message classifications, or conflicting polarity.

## Runtime corrections

- Bare `SET FOLLOW` now toggles `Normal` / `Blind`; explicit `SET FOLLOW BLIND|NORMAL` remains supported.
- `SET SPEECH FAST|SLOW` remains supported and bare `SET SPEECH` now toggles Fast / Slow.
- Bare `TALK` is **not** changed into a toggle; it still requires `TALK FAST|SLOW`.
- `SET MESSAGES LOW|MEDIUM|HIGH` now records only an explicitly selected native message level.
- `SET ENTRANCES ON|OFF` and bare `SET ENTRANCES` now record the selected/toggled state.
- Existing `GOSSIP`, `SET GOSSIP`, `SET GOSSIP ON|OFF`, `AUCTION`, `SET AUCTION`, and `SET AUCTION ON|OFF` runtime behavior already matched the retained native contract and is now promoted narrowly.

## Proven native message-level semantics

Native help 121 defines:

- Low: high-priority messages only.
- Medium: high- and medium-priority messages.
- High: high-, medium-, and low-priority messages.

The same native help explicitly says the exhaustive assignment of individual messages to those priorities is not listed. Pass 32 therefore does **not** invent a priority classification table or claim complete filtering parity.

## Evidence boundaries retained

- Native defaults and exact persistence ownership/serialization remain unresolved.
- `SET TELEPATH ON|OFF` exists, but exact blocking/receipt polarity remains evidence-gated.
- Retained `SET RECEIVE` help wording conflicts with the Profile help wording, so explicit RECEIVE polarity is not newly promoted.
- `SET ENTRANCES` exact login/logout/realm event rendering, timing, recipient selection, and native default remain unresolved.
- `MAXPAGES`, page/flood behavior, `SET INTERRUPT`, `SET WARNING`, and remaining exact communication rendering remain unresolved.
- Native FAST-mode command resolution remains separate from Reforged's operator-mandated global command-to-SAY fallback.

## Parity delta

- Before: 82 PASS / 76 STRONG / 28 UNKNOWN = 186.
- After: 88 PASS / 76 STRONG / 28 UNKNOWN = 192.
- Strict parity: 45.8%.
- Evidence-backed: 85.4%.
