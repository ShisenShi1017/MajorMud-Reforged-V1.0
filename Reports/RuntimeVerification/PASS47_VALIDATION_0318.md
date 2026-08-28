# PASS 47 Validation — Update 0318

## Scope

The retained WCCMMHLP command summary and explicit broadcasting aliases are projected through the Librarium compiler into generated Classic runtime data and checked against the live parser.

## Parity

- `commands.native-player-command-table`: UNKNOWN to STRONG.
- Score: 97 PASS / 88 STRONG / 15 UNKNOWN = 200.
- Strict parity: 48.5%.
- Evidence-backed parity: 92.5%.

## Verification

- Compiler build: PASS, 0 warnings / 0 errors.
- Compiler regression: PASS, 104,911 records across 47 test-world families.
- Clean Classic generation: PASS, 103,330 objects across 36 families and 41 files.
- Native player-command projection: 63 documented inputs and 3 explicit aliases.
- Server build: PASS, 0 warnings / 0 errors.
- Native player-command runtime smoke: PASS, 63/63 inputs and 3/3 aliases.
- PASS 31 native-help alias regression: PASS.
- PASS 43 MajorMUD Plus event boundary: PASS.
- PASS 44 party coordination boundary: PASS.
- PASS 45 Classic trap boundary: PASS.
- PASS 46 generated native-help boundary: PASS.
- Core/server smoke suite: PASS, 17/17.
- Python historical validators: not run because `py -3` reports no installed interpreter.

Undocumented aliases, native minimum prefixes, ambiguity precedence, hidden dispatcher entries, and exact argument-parser ordering remain unresolved. No behavior was guessed.
