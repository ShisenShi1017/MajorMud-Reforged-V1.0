# Pass 36 Validation — Update 0307

Status: **PASS (static/evidence validation)**

Pass 36 verifies the operator-supplied character modifier-order screenshots against retained MajorMUD evidence. The screenshots are treated as hypotheses only.

## Focused validation

`Tools/Validation/validate_pass36_character_modifier_order_verification.py`

- **92/92 PASS**
- Confirms promoted race-minimum creation-stat evidence.
- Confirms native `calculate_secondary_stats @0x0041a424` formula-specific recomputation evidence.
- Confirms retained signed parameter-delta ability records for Wisdom/Willpower, Strength, Health, Agility, and Charm without inventing an Intellect counterpart.
- Confirms additive decoded direct equipment combat fields while preserving the unresolved global order.
- Confirms AC-before-Dodge native combat resolution and the retained blind/debuff field boundary.
- Confirms the existing PASS three-second spell/status round and rejects a universal ten-second poison tick.
- Confirms only `character.cross-category-modifier-order` changes parity state: UNKNOWN -> STRONG.
- Confirms all audited gameplay modifier source files are byte-identical to the Pass 35 baseline.
- Confirms Borin Trainer, Custom1 Simple Editor, and protected WPF source hashes remain unchanged.
- Confirms no `WCCMMUD.INI` file exists in Reforged.

## Retained regressions

All retained cumulative parity validators pass after advancing the runtime audit to the 0307 ledger:

- Pass 15: 60/60 PASS
- Pass 24: 53/53 PASS
- Pass 26: 70/70 PASS
- Pass 27: 68/68 PASS
- Pass 28: 51/51 PASS
- Pass 29: 78/78 PASS
- Pass 30: 56/56 PASS
- Pass 31: 57/57 PASS
- Pass 32: 109/109 PASS
- Pass 33: 93/93 PASS
- Pass 34: 106/106 PASS
- Pass 35: 136/136 PASS
- Pass 36: 92/92 PASS

Librarium graph validation: **PASS** — 20,826 nodes / 71,026 relationships / 0 missing sources / 0 missing entity targets / 0 duplicate-edge diagnostics.

Generation readiness: **PASS** — 32 object families / 0 failures.

## Build boundary

A compiled .NET build was **not run** because this execution environment does not provide `dotnet`, `csc`, `msbuild`, `mono`, or `mcs`. Static checks are supplemental and are not represented as a compiled-build PASS.

## Parity after Pass 36

- PASS: **97**
- STRONG: **77**
- UNKNOWN: **26**
- Total: **200**
- Strict parity: **48.5%**
- Evidence-backed: **87.0%**

No gameplay formula was changed. The universal cross-category modifier order remains below PASS.
