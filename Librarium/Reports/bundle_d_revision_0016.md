# Bundle D Revision 0016 — One-Pass Remaining Exhaustion

This cumulative correction uses the uploaded repository with Bundles A–D installed.

## DONE
- Remaining decoded exit-state fields are retained by the authoritative RuntimeExitGateDefinition.
- Gate evaluation consumes class/race allow and deny rules, alignment bounds, ability min/max, ordered action requirements, picklock difficulty, and decoded strength alternatives.
- Reciprocal exit identity, reciprocal state-link metadata, state identity, owner scope, save-world-state risk, automatic routing, fallback behavior, time multiplier, and pre/post spell effects remain attached to the canonical map-qualified exit authority.
- Added a D-only semantic closure audit that excludes compiler/provenance/client metadata from gameplay closure.
- Final Bundle D audit now requires the D-only remaining-semantic audit and returns failure unless BundleDComplete is true.
- Installer validates A, B, C, every D phase, the remaining D semantic audit, final D closure, Classic runtime, and win-x64 publish.

## LEFT
- None within Bundle D when the executable D-only semantic and final closure audits pass.
- Project-wide decode-exhaustion disconnects belonging to later bundles are not treated as Bundle D failures.

## Files Added
- Apps/Source/Reforged.Server/RuntimeBundleDRemainingExhaustionAudit.cs
- Librarium/Reports/bundle_d_revision_0016.md

## Files Modified
- Apps/Source/Reforged.Server/Program.cs
- Apps/Source/Reforged.Server/RuntimeLivingWorldCompletion.cs
- Apps/Source/Reforged.Server/RuntimeBundleDFinalExhaustionAudit.cs

## Duplicate Paths Removed
- None. No proven exact duplicate runtime file was removed; Bundle H retains major consolidation ownership.

## Compile correction
- Corrected the `--audit-bundle-d-remaining` top-level command path to return an integer exit code instead of a bare `return;`.
- Audit success returns `0`; audit failure returns `1`.

## Installer correction
- Audit commands execute the RID-specific build output at `bin\\Release\\net8.0\\win-x64\\Reforged.Server.exe`.
- Quest ability provenance keys (`quest.ability.source.<id>`) are retained as metadata and excluded from numeric ability-value restoration.
