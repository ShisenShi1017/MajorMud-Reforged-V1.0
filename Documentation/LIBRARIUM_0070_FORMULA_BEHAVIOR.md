# CodeUpdate-0070 Formula Behavior Verification Report

Generated: 2026-07-08T19:49:13.601289+00:00

## Scope

This update starts formula behavior verification after CodeUpdate-0069. It does not promote formulas to Verified and does not replace runtime formulas. The purpose is to convert formula candidates into behavior-level assertions that can be tested later with simulator/runtime evidence.

## Results

- Behavior records created: **8**
- Supporting candidate references mapped: **98**
- Deterministic behavior probes created/executed: **3**
- Verified promotions: **0**
- Runtime formula replacements: **0**

## Behavior Assertions Created

### FB0070-RANDOM_ROUND_RANGE — random_range_round

- Evidence state: Derived
- Confidence: candidate_behavior_not_classic_verified
- Supporting candidates: 1
- Summary: Candidate behavior: sample a random value from min/max inputs then round result. Requires Classic RNG and rounding semantics before promotion.
- Runtime replacement: none

### FB0070-RESIST_PIPELINE — resist_reduction_pipeline

- Evidence state: Derived
- Confidence: candidate_behavior_not_classic_verified
- Supporting candidates: 25
- Summary: Candidate behavior: raw spell/hitspell damage may be reduced by magic resistance/anti-magic and later by resistance before accumulated as resisted damage. Requires exact CalcResistedDamage proof.
- Runtime replacement: none

### FB0070-DURATION_EXTRA_TICK — duration_extra_damage

- Evidence state: Derived
- Confidence: candidate_behavior_not_classic_verified
- Supporting candidates: 3
- Summary: Candidate behavior: attack/spell duration greater than two can add another max hitspell/duration contribution. Requires Classic tick duration interpretation.
- Runtime replacement: none

### FB0070-PER_ROUND_SUM — per_round_damage_sum

- Evidence state: Derived
- Confidence: candidate_behavior_not_classic_verified
- Supporting candidates: 45
- Summary: Candidate behavior: displayed damage-per-round sums local attack damage and local spell value. UI-derived; not a gameplay equation by itself.
- Runtime replacement: none

### FB0070-SPAWN_COMPLEMENT — spawn_probability_complement

- Evidence state: Derived
- Confidence: candidate_behavior_not_classic_verified
- Supporting candidates: 2
- Summary: Candidate behavior: lair spawn probability modeled as complement probability over max regen attempts. Requires server/runtime spawn evidence.
- Runtime replacement: none

### FB0070-EXP_PER_REGEN_TIME — exp_per_time

- Evidence state: Derived
- Confidence: candidate_behavior_not_classic_verified
- Supporting candidates: 17
- Summary: Candidate behavior: experience/hour model scales experience by inverse regen time. Tool/model behavior; requires Classic runtime timing proof.
- Runtime replacement: none

### FB0070-OOM_ROUNDS — rounds_to_oom

- Evidence state: Derived
- Confidence: candidate_behavior_not_classic_verified
- Supporting candidates: 2
- Summary: Candidate behavior: out-of-mana rounds incorporate heal cost, attack spell cost, and bless cost/tick. Requires exact tick/cost semantics.
- Runtime replacement: none

### FB0070-THREE_SECOND_TICK — three_second_tick

- Evidence state: Derived
- Confidence: candidate_behavior_not_classic_verified
- Supporting candidates: 3
- Summary: Candidate behavior evidence states some bless calculations use 3-second round ticks instead of 5. Requires Classic tick timing confirmation.
- Runtime replacement: none

## Why nothing was promoted

Promotion still requires proof of exact Classic behavior. The candidate sources show useful implementation behavior, but the remaining unknowns include RNG boundaries, rounding mode, tick timing, duration semantics, resistance order, and whether UI/model calculations match actual server behavior.

## Next Work

CodeUpdate-0071 should run targeted simulator/runtime scenarios for the highest priority behavior records: resistance pipeline, duration/tick timing, random damage rounding, and spawn probability.
