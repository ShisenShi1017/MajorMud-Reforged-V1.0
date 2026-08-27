# Pass 9 Validation — Character / Advancement

## Available validation results

- **PASS** — Pass 9 static source/formula/parity checks: 22/22.
- **PASS** — `Librarium/Tools/validate_librarium.py`.
- **PASS** — `Tools/Librarium/validate_generation_readiness.py`.
- **PASS** — `Tools/Validation/validate_combat_runtime.py`.
- **PASS** — `Tools/Validation/validate_combat_runtime_part2.py`.
- **PASS** — Gameplay Group 4 validator.
- **PASS** — WCC Librarium exhaustion audit; 26,670/28,872 room descriptions represented and no WCCMP files missing from that audit's expected source set.

## Known / pre-existing unavailable-data failures

Gameplay Groups 1–3 still fail for the handoff-documented reasons and are **not Pass 9 regressions**:

- Group 1: missing `Data/Classic/skills.json` and `Data/Classic/npcs.json`.
- Group 2: missing `Data/Classic/quests.json` and `Data/Classic/scripts.json`.
- Group 3: missing `Data/Classic/shops.json`, `banks.json`, and `economy_rules.json`.

The semantic-exhaustion audit likewise cannot run to completion because packaged `Data/Classic/quests.json`, `triggers.json`, and `scripts.json` are absent. The Sim leveling/regeneration validators fail at their data-load boundary because `Data/leveling.json` and other legacy Sim data are absent, not because a Pass 9 formula assertion failed. Several older economy validators require evidence/manifest files not present in this cumulative package and fail at that prerequisite boundary.

## Tooling unavailable

`dotnet`, `csc`, `mcs`, and `msbuild` are unavailable in the packaging environment. A local .NET build is therefore required for final compiler validation.
