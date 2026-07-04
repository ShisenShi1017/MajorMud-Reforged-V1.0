# CodeUpdate-0063 - Generic Scheduler Execution Channels

## Purpose

Correct the CodeUpdate-0062 scheduler bootstrap surface so scheduler channels are generic engine execution channels instead of MajorMUD/gameplay-specific channel names.

This update also folds the user-supplied ServerRuntimeValidation0009 handoff into the no-.NET simulator validation surface. That simulator work remains expected-model validation only and does not activate Server gameplay.

## Changed

- Replaced gameplay-facing diagnostic scheduler channel names with generic host execution channels.
- Updated smoke coverage to verify the generic channel set.
- Updated ServerSim0036 scheduler mapping to clarify that gameplay-facing findings are evidence only and should not hardcode the reusable scheduler.
- Preserved curated DataSimVer0045 evidence-regression contract planning under `Docs/ServerSim/DataSimVer0045/`.
- Folded ServerRuntimeValidation0009 into the no-.NET simulator by adding explicit combat lifecycle state, death-triggered expected-model respawn scheduling, and canonical monster fallback use for simulator monster projections.
- Preserved ServerRuntimeValidation0009 handoff notes under `Docs/ServerSim/ServerRuntimeValidation0009/`.
- Updated continuity documents and manifests to CodeUpdate-0063.

## Generic Channels

- Ingress.
- Validation.
- Planning.
- Dispatch.
- Mutation.
- TimedWork.
- EventWork.
- Persistence.
- Observation.
- Maintenance.

## Boundaries

- No scheduler ticking.
- No world loop.
- No sockets.
- No sessions.
- No gameplay.
- No combat execution.
- No movement execution.
- No spawning.
- No status-effect ticking.
- No script/event dispatch.
- No AI execution.
- No runtime rule execution.
- No mode/profile activation.
- No canonical `Data/` rewrite.
- No imported/reference deletion.
- No runtime formula or timing promotion.
- No execution-event adapter or replay harness implementation.
- No autonomous NPC aggro or assist behavior.
- No live Worldgroup parity claim from simulator output.

## Verification

Local verification passed:

- JSON parse for `ProjectManifest.json` and `UpdateManifest.json`.
- `git diff --check`.
- Sequential Release build sweep for Core, Server, Importer, Editor, Launcher, Client, and Tests.
- Core/server smoke tests.
- Server bootstrap run.
- Direct Python simulator smoke functions.
- Focused simulator lifecycle marker probe for `*Combat Engaged*`, `*Combat Off*`, and expected-model respawn due minute scheduling.

`pytest` is not installed locally, so pytest-based simulator execution was not run.

GitHub Actions remains the official validation gate after the user pushes this update.
