# PASS 46 Validation — Update 0317

## Scope

The complete retained WCCMMHLP corpus is projected through the Librarium compiler into generated Classic runtime data. Runtime help loading now reads `Data/Classic/native_help.json` only.

## Parity

- `native.help-reference`: UNKNOWN to STRONG.
- Score: 97 PASS / 87 STRONG / 16 UNKNOWN = 200.
- Strict parity: 48.5%.
- Evidence-backed parity: 92.0%.

## Verification

- Compiler build: PASS, 0 warnings / 0 errors.
- Compiler regression: PASS, 104,848 records across 46 test-world families.
- Clean Classic generation: PASS, 103,267 objects across 35 families and 40 files.
- Native help projection: 1 main record plus 131 topic records.
- Server build: PASS, 0 warnings / 0 errors.
- Core/server smoke suite: PASS, 17/17.
- PASS 43, PASS 44, and PASS 45 focused .NET smoke tests: PASS.
- PASS 31 native-help alias regression: PASS.
- PASS 43 MajorMUD Plus event smoke: PASS.
- PASS 44 party coordination smoke: PASS.
- PASS 45 Classic trap smoke: PASS.
- PASS 46 generated native-help smoke: PASS.
- Python historical validators: not run because the installed Python launcher reports no registered interpreter.

Exact HELP TOPICS rendering, prefix/fuzzy resolution, ambiguity precedence, pagination, textblock linkage, and permission filtering remain unresolved. No behavior was guessed.
