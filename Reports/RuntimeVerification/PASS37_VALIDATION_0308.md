# Pass 37 Validation — Update 0308

Status: **PASS (static/evidence validation)**

Pass 37 corrects the stock Classic REST/MEDITATE regeneration path and promotes `native.rest-inn-recovery-policy` from UNKNOWN to STRONG.

## Focused validation

`Tools/Validation/validate_pass37_classic_regeneration_correction.py`

- **110/110 PASS**
- Confirms exact native WCCMMHLP message 205 for the 20-second activation boundary and movement/attack/being-attacked interruption behavior.
- Confirms retained stock `/750` HP base and explicitly rejects the GreaterMUD `/500` branch for Classic.
- Confirms REST multiplies the stock HP base by three before HPRegen percentage.
- Confirms types 1-4 MEDITATE exits before MPRegen percentage while passive mana applies MPRegen.
- Confirms Kai/type 5 retains its one-point MPRegen branch.
- Confirms strong 30-second passive, 20-second REST, and 10-second MEDITATE cadence evidence while preserving exact host phase as unresolved.
- Confirms only `native.rest-inn-recovery-policy` changes state: UNKNOWN -> STRONG.
- Confirms Borin Trainer, Custom1 Simple Editor, protected WPF sources, and the no-`WCCMMUD.INI` boundary remain intact.

## Retained regressions

- Pass 15: **60/60 PASS**
- Pass 24: **53/53 PASS**
- Pass 26: **70/70 PASS**
- Pass 27: **68/68 PASS**
- Pass 28: **51/51 PASS**
- Pass 29: **78/78 PASS**
- Pass 30: **56/56 PASS**
- Pass 31: **57/57 PASS**
- Pass 32: **109/109 PASS**
- Pass 33: **93/93 PASS**
- Pass 34: **106/106 PASS**
- Pass 35: **136/136 PASS**
- Pass 36: **92/92 PASS**
- Pass 37: **110/110 PASS**

Librarium graph validation: **PASS** — 20,826 nodes / 71,026 relationships / 0 missing sources / 0 missing entity targets / 0 duplicate-edge diagnostics.

Generation readiness: **PASS** — 32 object families / 0 failures.

A lightweight delimiter scan of all Pass 37 changed/new C# files found balanced code delimiters. This is not represented as a compile.

## Build boundary

A compiled .NET build was **not run** because this execution environment does not provide `dotnet`, `csc`, `msbuild`, `mono`, or `mcs`.

## Parity after Pass 37

- PASS: **97**
- STRONG: **78**
- UNKNOWN: **25**
- Total: **200**
- Strict parity: **48.5%**
- Evidence-backed: **87.5%**

Exact Worldgroup host-clock phase, simultaneous tick ordering, exact MEDITATE presentation text, and remaining cleanup ordering stay below PASS.
