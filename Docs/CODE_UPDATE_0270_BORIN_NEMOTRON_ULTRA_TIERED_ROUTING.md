# Code Update 0270 - Borin Nemotron Ultra Tiered Routing

## Result

Borin now has one authoritative brain with three reasoning tiers: Local, NVIDIA Lightning, and NVIDIA Ultra. Lightning remains the normal hosted model. Ultra adds a separate deep-reasoning tier using `nvidia/nemotron-3-ultra-550b-a55b`.

## Credentials

Windows Credential Manager stores two independent targets:

- Lightning preserves the existing `MajorMudReforged/Borin/NvidiaNim` target.
- Ultra uses `MajorMudReforged/Borin/NvidiaNim/Ultra`.

Neither key is persisted in Forge JSON, source, logs, prompts, diagnostics, or UI state. The UI never reads a stored key back for display.

## Routing

- Local: deterministic gameplay, parsing, and known state transitions.
- Lightning: conversation, normal gameplay reasoning, routine development/Sysop work, and issue triage.
- Ultra: architecture tasks, cross-file evidence, large contexts, build corrections, conflicting evidence, explicit deep reasoning, and deep tasks after repeated Lightning failures.

Selection uses structured task metadata including purpose/role, bounded context size, distinct evidence-file count, correction state, architecture markers, conflicting evidence, and prior Lightning failures. Keywords alone do not decide the tier.

Low-confidence Lightning output may escalate exactly once to Ultra. Ultra failure retains a usable Lightning answer or falls back with the same `BrainContext`. Ordinary Lightning failure falls back locally and does not consume Ultra merely to evade a rate limit.

## Rate control

Lightning and Ultra have independent sliding-window budgets plus one overall NVIDIA scheduler. Lightning retains its 40-RPM hard budget. Ultra uses a separate conservative configured safety budget of 10 RPM; when NVIDIA returns request-limit headers, the UI reports the observed endpoint limit separately rather than claiming the configured safety cap is the provider limit.

## Forge UI

The existing Local Brain & NVIDIA menu now contains independent masked key slots, Change/Test actions, connection status, rate status, automatic-escalation setting, overall scheduler status, and last route/key-slot/reason telemetry.

## Verification

- NVIDIA/provider/authority harness: 41/41 PASS.
- Coverage includes distinct credential targets, Local/Lightning/Ultra choice, bounded escalation, same-context fallback, independent key failures, independent rate accounting, reported endpoint limits, and configuration persistence.

