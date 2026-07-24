# Remaining Systems Update 0034 — Error Handling and Recovery

## Implemented

- Central failure classification and recovery decisions.
- Fail-closed missing-reference and unknown-failure behavior.
- Validated-default use only when explicitly available.
- Rollback requests only when the owning subsystem declares rollback support.
- Critical-load and duplicate-runtime-ID stop-world-load policy.
- Unrecoverable persistence/script/event/reset failures can disable the owning subsystem.
- Character-scoped unknown failures route to disconnect rather than unsafe continuation.
- Structured audit requirement on every decision.

## Ownership

The recovery service selects policy. Owning subsystems execute rollback, persistence, disconnection, or shutdown. Logging remains owned by RuntimeLoggingDiagnosticsService.

## Native dependencies

Exact MajorMUD invalid-record, corrupt-save, partial-load, script, event, reset, and persistence failure behavior remains for the final native pass.
