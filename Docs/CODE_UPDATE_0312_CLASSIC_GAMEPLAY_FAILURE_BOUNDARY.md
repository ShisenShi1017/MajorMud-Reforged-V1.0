# Code Update 0312 — Classic gameplay failure boundary

Pass 41 closes the broad ownership split for ordinary rejected Classic gameplay actions without inventing native failure text or persistence recovery.

## Promoted boundary — STRONG

`native.error-handling-recovery` advances **UNKNOWN → STRONG**.

An ordinary rejected gameplay action is state-preserving: it is a no-op, does not request rollback, does not disable the gameplay subsystem, does not stop world load, and does not disconnect the character merely because the action was rejected. This is the same narrow Classic behavior first activated in Pass 33, now promoted into the broad error-handling/recovery assertion because the surrounding ownership branches are explicitly separated.

This does **not** mean every failure is recoverable. Wrong/corrupt native MajorMUD files or wrong versions remain catastrophic startup/load failures and commonly require a Worldgroup restart. Reforged must not turn those into graceful in-game recovery.

Reforged transactional rollback helpers may remain for atomic internal operations and Custom/AI behavior, but they are not evidence that native MajorMUD ran a generic rollback subsystem for ordinary rejected commands.

## Still UNKNOWN

- exact command/action-specific failure text versus silent no-op;
- malformed/missing MajorMUD data-reference behavior reached after successful startup;
- catastrophic save participant and rollback ordering;
- Worldgroup host/session crash recovery (outside MajorMUD gameplay parity).

No generic synthetic Classic error message was added. No `WCCMMUD.INI` file was added.

## Score

Before Pass 41: **97 PASS / 81 STRONG / 22 UNKNOWN = 200**.

After Pass 41: **97 PASS / 82 STRONG / 21 UNKNOWN = 200**.

- strict parity: **48.5%**
- evidence-backed: **89.5%**
