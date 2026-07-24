# MajorMUD Reforged — Bundle D Revision 0004

## DONE

Phase D4 Doors completed as an exhaustive evidence-gated runtime pass.

- Audited every generated exit whose `gate_kind` is `door`.
- Verified one live door definition and one persisted runtime state per decoded door exit.
- Confirmed live movement checks authoritative door state and blocks locked or closed doors.
- Confirmed `open`, `close`, `lock`, and `unlock` mutate the same living-world state used by traversal.
- Preserved generated key-item prerequisites and enforced ownership when explicit.
- Validated canonical map-qualified source and destination room ownership.
- Added an executable Bundle D door audit and retained the advanced door state-machine smoke validation.
- Kept unresolved pick, bash, magical-lock, quest-lock, broken-door, linked-door, and reset behavior evidence-only unless explicit generated values exist.

## LEFT

Phase D5 Search System.

## VERIFIED

- Generated exit identity is authoritative door identity.
- Live traversal consumes persisted living-world door state.
- Locked doors block before closed-door evaluation.
- Open, close, lock, and unlock operate on the live runtime state.
- Explicit generated key IDs are required for lock and unlock.
- Canonical room IDs own door endpoints.

## STRONGLY CORROBORATED

- The advanced door policy service correctly models linked state, broken state, timed reset, restrictions, key consumption, pick, and bash ordering.
- Unqualified Classic doors use a closed, unlocked initial state rather than invented lock data.

## DECODED BUT NOT MAPPED

- Picklock/strength thresholds that remain null in generated data.
- Magical locks, quest locks, broken state, linked doors, and reset timers without explicit generated values.

## CONFLICTS

- The repository contains a richer door policy engine and a living-world live-state consumer. Revision 0004 documents and validates their boundary without creating a third authority; final consolidation remains assigned to Bundle H after generated advanced fields are available.

## TRUE UNKNOWN

- Exact native pick/bash formulas and unclassified door qualifier meanings.

## Files Added

- `Apps/Source/Reforged.Server/RuntimeBundleDDoorAudit.cs`
- `Librarium/Reports/bundle_d_revision_0004.md`

## Files Modified

- `Apps/Source/Reforged.Server/Program.cs`
- `Apps/Source/Reforged.Server/RuntimeLivingWorld.cs`
- `INSTALL_AND_VALIDATE.bat`

## Files Removed

None.

## Duplicate Paths Removed

None. No unproven original runtime file was removed.

## Build Result

Performed by `INSTALL_AND_VALIDATE.bat`: Release win-x64, warnings as errors, zero warnings and zero errors required.

## Audit Result

Adds `--audit-bundle-d-doors` and runs all cumulative Bundle A–D audits.

## Publish Result

Performed by installer to `Release\MajorMudReforged`, including verification of `Reforged.Server.exe`.
