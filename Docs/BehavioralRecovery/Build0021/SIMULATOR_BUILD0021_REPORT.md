# SimulatorBuild0021 — Behavioral Inference & Formula Mining

Status: COMPLETE (single-pass behavioral recovery; static/inferred evidence only)

Runtime used: no. Live Worldgroup validation: no. Original behavior certified from runtime: no.

## Scope

This pass mined the uploaded Build0020 simulator evidence, the current MajorMUD Reforged repository, the repository `ImportedDatabases` folder, and the installed Worldgroup server archive. It does not fabricate runtime results. Any behavior that requires observing the live server remains `runtime_required`.

## Verified input artifacts

- MajorMUD-Reforged-SimulatorBuild0020-FullSystemsSimulationMatrix.zip: passed; entries=859; sha256=435e47a92fbd5b54b3b35a0c181cc6de6eeb1ab8e2131802edf95a1c1458ac8b
- MajorMud-Reforged-V1.0-main(15).zip: passed; entries=4823; sha256=013ce36bebdc39116308e6c6fb86dacf7331d98735f8290e472e9279d8aa80fa
- Worldgroup Server(1).zip: passed; entries=3392; sha256=5e98e61aa8abc9dbf1c811f3d36c4943424bc0aa7a8d9df2a98bc16e55a95e78

## Evidence coverage

- Imported item records: 1950
- Imported monster records: 1100
- Imported spell records: 1350
- Imported shop records: 178 with 3550 inventory rows
- Imported class records: 15
- Imported race records: 13
- Repo item records: 1770
- Repository + simulator CSV evidence files counted: 335
- Extracted Worldgroup server files counted: 3064

## Main recovery result

The pass recovered **static behavior inputs** much more strongly than full live formulas. Combat, spells, shops, equipment, monster rewards, class modifiers, and race modifiers all have decoded fields that can drive an expected-model simulator. However, random-roll resolution, order of operations, timing, target selection, state flags, transaction edge cases, and live progression thresholds remain unresolved without runtime observation.

## Confidence counts

- verified_from_data: 6
- strong_candidate: 4
- runtime_required: 4
- weak_candidate: 2

## Simulator update decision

No executable simulator formula was promoted as certified original behavior in this pass. The safe update is the generated `BehavioralFormulaCatalog_Build0021.csv` and related evidence matrices. Formula fields may be consumed by the simulator as expected-model inputs, but all live-resolution formulas remain clearly labeled by confidence.

## Key findings

1. **Combat:** decoded data supports combat inputs (monster HP/AC/DR/MR/XP, item damage/AC/DR/accuracy/speed, class combat/weapon/armor bonus), but not the actual hit/dodge/crit/damage roll equations.
2. **XP:** monster base XP and race/class XP modifiers are verified static fields; actual threshold and sharing/penalty formulas are runtime-required.
3. **Equipment:** item slots, restrictions, requirements, and numeric bonuses are verified from data; stacking/order/special-use behavior is not certified.
4. **Spells:** costs/effect/range/duration/scaling columns are verified static fields; cast success, resist, tick cadence, and stacking behavior require runtime.
5. **Shops:** inventory and markup fields are verified; exact buy/sell, charisma, repair, training, and banking behavior remains runtime-required.
6. **Monsters:** per-record stats should be used directly; correlations do not justify a universal stat-scaling formula.
7. **Quests:** static dialogue/script candidates exist from prior RuntimePrep evidence, but the current repo quest data is empty and state transitions are runtime-required.

## Files produced

- `BehavioralFormulaCatalog_Build0021.csv`
- `EvidenceSourceCoverage_Build0021.csv`
- `InputArtifactVerification_Build0021.csv`
- `MonsterCorrelationMatrix_Build0021.csv`
- `RuntimeRequiredMatrix_Build0021.csv`
- `SIMULATOR_BUILD0021_METRICS.json`
- `SIMULATOR_BUILD0021_REPORT.md`
- `SIMULATOR_BUILD0021_HANDOFF.md`
- `SIMULATOR_BUILD0021_VERIFICATION.json`
