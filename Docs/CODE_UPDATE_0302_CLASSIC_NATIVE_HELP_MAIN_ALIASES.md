# Code Update 0302 — Classic Native HELP Main Aliases

Pass 31 promotes only the exact HELP aliases printed on retained WCCMMHLP message 1 that were not already safely resolved by the runtime: `LAWS`, `MISC`, `SET`, and `HELP`.

## Runtime changes

- `HELP LAWS` -> native body 31.
- `HELP MISC` -> native body 111.
- `HELP SET` -> native body 117.
- `HELP HELP` -> native body 263.
- Native bodies remain returned verbatim, including retained ANSI.

## Evidence boundary

`HELP TOPICS` is explicitly named by native help, but the exact topic-list rendering is not present in retained evidence and remains fail-closed. Native fuzzy/prefix matching, ambiguity precedence, permission filtering, textblock linkage, and pagination also remain UNKNOWN.

## Parity delta

- Before: 78 PASS / 76 STRONG / 28 UNKNOWN = 182.
- After: 82 PASS / 76 STRONG / 28 UNKNOWN = 186.
- Strict parity: 44.1%.
- Evidence-backed: 84.9%.
