# CodeUpdate 0068 — Formula Reconciliation Report

## Status
PASS_WITH_FINDINGS. The Librarium remains canonical. MMUD Explorer and MBBSEmu were used only as external evidence sources. No runtime formulas were replaced.

## Scope Completed
- Formula candidate inventory across MMUD Explorer and MBBSEmu.
- Subsystem classification for combat, XP, regeneration, loot, spawn/tick timing, spell/skill, and stats candidates.
- Cross-reference keyword mapping against existing Librarium formula material.
- Potential conflict registry for same-name formula assignments across external sources.
- Knowledge gap registry update for formula-critical unknowns.

## Source Scan Summary
- MMUD Explorer files scanned: 51
- MMUD Explorer candidate lines found: 7936
- MBBSEmu files scanned: 487
- MBBSEmu candidate lines found: 2605
- Candidate lines selected for reconciliation index: 1500
- Potential conflicts requiring manual review: 2

## Subsystem Counts
- combat: 860
- loot: 10
- regen: 248
- spawn: 53
- spell_skill: 479
- stats: 414
- unclassified: 40
- xp: 403

## Promotion Result
No formula was promoted to Verified. This is intentional: external repository code is strong evidence, but not canonical Classic proof by itself. Candidates are registered as Derived and must be corroborated with reference evidence, simulator evidence, or observed runtime behavior.

## Highest Priority Remaining Formula Gaps
- Combat equations
- Hit/defense resolution
- Backstab resolution
- Damage rounding
- XP equations
- Regen equations
- Loot equations
- Spawn and tick timing

## Files Added
- `/Evidence/FormulaReconciliation0068/formula_candidate_inventory_0068.json`
- `/Evidence/FormulaReconciliation0068/potential_formula_conflicts_0068.json`
- `/Evidence/FormulaReconciliation0068/external_formula_source_summary_0068.json`
- `/Librarium/FormulaReconciliation/formula_reconciliation_index_0068.json`
- `/Librarium/GapRegistry/formula_gap_registry_0068.json`
- `/Validation/FormulaReconciliation0068/validation_0068.json`

## Next Recommended Work
CodeUpdate-0069 should run targeted simulator/reference checks against the top combat and damage-rounding candidates first, because those affect the largest number of runtime consumers.
