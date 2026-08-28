# PASS 49 Validation - Update 0320

## Scope

Recover and execute the exact Classic ordinary-death experience boundary, recover the final-death/reroll retention formula, and keep the disconnected replacement-character consumer explicit.

## Parity

- `character.death-xp-loss-exact`: UNKNOWN to STRONG.
- Score: 97 PASS / 90 STRONG / 13 UNKNOWN = 200.
- Strict parity: 48.5%.
- Evidence-backed parity: 93.5%.

## Focused Verification

- Server Release build: PASS, 0 warnings / 0 errors.
- PASS49 death-experience smoke: PASS, 10/10.
- Ordinary death preserves XP and records zero loss: PASS.
- Final-reroll half-level threshold and 10% candidate selection: PASS.
- Player/global KEEPEXP gates and level-one zero result: PASS.
- Absence of a 2,000,000 cap: PASS.
- Final reroll consumer remains gated: PASS.

## Cumulative Verification

- Full solution Release build with warnings as errors: PASS, 0 warnings / 0 errors.
- Compiler regression: PASS, 104,911 records across 47 families.
- Classic generation: PASS, 103,330 objects across 36 families and 41 files.
- Room descriptions: PASS, 26,668/26,668 non-empty descriptions.
- PASS31, PASS39, and PASS43-49 runtime smokes: PASS.
- Native help and native player-command smokes: PASS; all 63 documented commands and three explicit aliases resolve.
- Core/server smoke: PASS, 17/17.
- Python PASS49 validator: NOT RUN because no interpreter is registered locally.

GitHub Actions remains pending until the user commits and pushes this cumulative local update.
