# Session Handoff — CodeUpdate 0101 Combat Reconstruction

Current baseline after this package: **CodeUpdate 0101 Combat Reconstruction Branch**.

## Completed

- Full combat branch pass: inventory, mapping, reconciliation, formula/behavior review, probes, runtime traceability, promotion review, documentation.
- Combat evidence index: `Evidence/CodeUpdate0101_CombatReconstruction/combat_evidence_hits.csv`.
- Relationship index: `Generated/CombatReconstruction/combat_relationship_index.json`.
- Probe definitions: `Simulator/CodeUpdate0101_CombatReconstruction/combat_probe_definitions.json`.
- Gap registry: `Validation/CodeUpdate0101_CombatReconstruction/combat_gap_registry_0101.json`.
- Promotion decisions: `Validation/CodeUpdate0101_CombatReconstruction/combat_promotion_decisions_0101.json`.

## Results

- Raw combat evidence hits: 44050.
- Prioritized evidence hits: 1200.
- Relationship edges: 2771.
- Probe definitions: 6.
- Verified promotions: 0.
- Runtime formula replacements: 0.

## Why no promotions

The current runtime formulas are deterministic Reforged formulas and remain explicitly `not_classic_claimed`. Classic combat verification still needs exact evidence for hit chance, damage, armor/order, critical/backstab, rounding, and timing.

## Recommended next branch

Continue with **Magic Reconstruction** if following subsystem order, or perform a **Combat Proof Pass** if the user wants to stay on combat until one or more gaps can be closed.
