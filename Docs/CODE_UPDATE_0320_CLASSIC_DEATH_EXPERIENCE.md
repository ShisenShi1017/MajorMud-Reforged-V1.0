# CodeUpdate 0320 - Classic Death Experience Correction

## Scope

Pass 49 exhausts the native WCCMMUD death-experience path and corrects the long-standing hypothesis that ordinary death removes experience.

## Native Result

- Exported `_check_kill_user` at RVA `0x00019A33` decrements lives and performs normal death cleanup without reading or mutating experience.
- Ordinary non-final death therefore loses exactly zero experience at every level.
- Exported `_save_permanent_info` at RVA `0x00071E9C` owns the previously misidentified 10% arithmetic.
- When global `KEEPEXP` and the player Keep option are enabled and the departing character is above level 1, final death/reroll retains:

  `max(stock XP threshold for floor(level / 2) + 1, floor(total XP / 10))`

- Otherwise the replacement character retains zero.
- The recovered routine contains no 2,000,000 cap and no ordinary-death de-level operation.

## Runtime Boundary

`RuntimeCharacterDeathResolver` now records the proven zero-loss ordinary-death transition. `RuntimeClassicDeathExperienceAuthority` owns the exact final-reroll calculator.

The current server does not yet replace a final-life character or transfer retained XP into a new character. The assertion therefore advances from UNKNOWN to STRONG, not PASS.

## Evidence

- Native artifact SHA-256: `5A99E7C34CF9CF58DC6E97FC3EF28F1BD83F9BCEC15CA59AD08826D2F21D2077`
- Evidence ledger: `Librarium/Parity/majormud_classic_death_experience_evidence_0320.json`
- Focused validation: `Reports/RuntimeVerification/PASS49_VALIDATION_0320.md`
