# Code Update Note — Build0021 Behavioral Recovery Evidence

This repository update imports the Build0021 behavioral recovery evidence package.

## Status

Build0021 is an evidence/catalog update only. It does not certify live Worldgroup runtime behavior.

## Key boundary

The following remain `runtime_required`:

- hit chance, dodge, critical hits, backstab, and final damage roll resolution
- XP level thresholds and runtime XP sharing/penalty rules
- quest state transitions
- skill advancement and training-cost formulas
- shop buy/sell spread, charisma/haggle effects, repair, banking, and training transactions

## Primary evidence package

See:

- `Docs/BehavioralRecovery/Build0021/SIMULATOR_BUILD0021_REPORT.md`
- `Docs/BehavioralRecovery/Build0021/BehavioralFormulaCatalog_Build0021.csv`
- `Docs/BehavioralRecovery/Build0021/RuntimeRequiredMatrix_Build0021.csv`
- `Docs/BehavioralRecovery/Build0021/SIMULATOR_BUILD0021_VERIFICATION.json`

## Commit intent

Preserve the cumulative evidence chain and make Build0021 discoverable from both docs and checkpoint paths.
