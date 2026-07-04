# DataSimVer0045 Evidence Regression Contract

This folder preserves curated planning material from `DataSimVer0045-EvidenceRegressionContract-WorkOnly.zip`.

It is evidence and planning material only. It does not modify canonical `Data/`, does not promote runtime values, does not certify live Worldgroup behavior, and does not enable gameplay.

## Server-Useful Contract

Validated evidence should become server-facing regression tests by comparing typed expected execution events to typed actual server execution events.

The future server does not need to match simulator internals, but it should eventually emit comparable events through an adapter layer so evidence can be replayed against the real implementation.

## Required Future Flow

1. Parse evidence logs into expected execution events.
2. Run a server scenario against the same repository `Data/` snapshot.
3. Emit actual execution events through a server adapter.
4. Compare expected and actual events in a replay harness.
5. Classify the result as `PASS`, `CANDIDATE`, `CONTRADICTION`, or `INSUFFICIENT`.

## Regression Case Shape

- `test_id`
- `evidence_source`
- `module_id`
- `world_profile`
- `data_version`
- `expected_events`
- `allowed_variance`
- `classification`
- `notes`

## Initial Test Families

- Combat death ordering.
- Combat retarget transition.
- XP credit by target engagement or credit set.
- Instant regen limited to cave worm and slime beast stock spawn points.
- Stealth state channel and backstab eligibility.
- Silver rapier blind proc timing.

## CodeUpdate-0063 Use

CodeUpdate-0063 uses this contract as planning evidence for keeping scheduler bootstrap channels generic. Generic channels can later carry typed execution events for validation without naming MajorMUD gameplay concepts in the reusable server host.

