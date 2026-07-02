# CodeUpdate-0052 Build0027 Current-State Handoff Report

## Purpose

CodeUpdate-0052 preserves the user-provided Build0027 current-state handoff so the repository reflects today's recovery work without relying on fragile chat memory.

This update is documentation/evidence preservation only. It does not add simulator executable harness code, rewrite canonical `Data/`, promote formulas, change server behavior, add gameplay, or certify original Worldgroup runtime behavior.

## Source Package

- Source ZIP: `C:\Users\djkao\Downloads\MajorMUD-Reforged-CodexHandoff-CurrentState-Build0027.zip`
- SHA-256: `7932FA3E6D4664D5440C0039CBF7FD85528CB0A5CBC09C7BE3C25CB2E4CF750F`
- Entries: 17

## Preserved Paths

- `DataDecodeCheckpoints/CurrentStateBuild0027/`
- `Docs/BehavioralRecovery/Build0027/`

## Key Build0027 Rules

- Original Worldgroup/MajorMUD runtime behavior must not be claimed unless supported by decoded source/data or observed runtime.
- Build0024 and Build0025 matrices remain `expected_model` unless converted into actual executable simulator tests.
- Earlier chat pass/fail claims are not authoritative unless backed by logs/files in artifacts or by locally generated Codex test output.
- Unresolved formulas and dynamic behavior remain `runtime_required`.

## Current-State Summary

Build0027 records:

- Static data/content recovery: effectively complete.
- Entity/world/catalog recovery: effectively complete.
- Simulator framework: present and partially runnable.
- Expected-model matrices: present for Build0024 and Build0025.
- Original runtime validation: unavailable.
- Remaining behavior work: convert expected-model rows into real executable simulator tests and implement missing formula hooks only where supported by source/data.

## Runtime-Required Areas

The handoff keeps these areas unresolved unless source-backed logic or runtime observation is recovered:

- Exact hit chance.
- Damage variance.
- Critical-hit chance and multiplier.
- RNG sequence/seed behavior.
- Mitigation ordering.
- Spell fizzle/resist/interrupt behavior.
- Healing scaling.
- Sneak/backstab probability and failure effects.
- Training cost formula.
- Banking edge behavior.
- Dynamic shop quirks.
- Tick/event scheduling.
- Dynamic quest/event state mutation edge cases.

## Verification

- CodeUpdate-0051 GitHub Actions is green by user report.
- Build0027 ZIP was inspected before copying.
- Build0027 ZIP SHA-256 was recorded.
- Extracted Build0027 files matched `MANIFEST.json` size/hash entries.
- Checkpoint copy under `DataDecodeCheckpoints/CurrentStateBuild0027/` matched the handoff manifest.
- JSON and CSV parse validation passed.
- Local sequential Core and Core.Tests Release builds passed with 0 warnings and 0 errors.
- Core/server smoke tests passed.
- An earlier parallel build attempt hit a transient shared `obj` file lock.

## Next Recommended Step

After user push and green GitHub Actions, CodeUpdate-0053 should start the Build0028 real executable harness foundation:

- Locate runnable simulator entry points.
- Run existing simulator tests without modification.
- Commit only logs/reports produced by actual runs.
- Add executable smoke tests only where hooks exist.
- Add skipped/xfail tests for missing hooks with `runtime_required` or `not_implemented` labels.
