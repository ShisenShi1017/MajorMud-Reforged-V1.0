# Classic gameplay failure boundary — Pass 41 / Update 0312

## Established

Ordinary rejected Classic gameplay actions preserve state. Pass 41 verifies that the central recovery decision is `NoOp` even when a caller says rollback is available and the failure is character-scoped. Applying the decision does not disable the Classic gameplay subsystem.

The catastrophic native-file boundary remains separate: wrong/corrupt/wrong-version native MajorMUD files are hard startup/load failures, not ordinary command failures.

## Evidence discipline

Reforged contains transactional rollback mechanisms for internal atomicity. Those mechanisms are not promoted as native MajorMUD behavior. Classic parity uses them only where a separately recovered gameplay contract requires the resulting atomic state, not as proof of a universal native recovery subsystem.

## Open evidence gates

Exact per-command failure text versus silence, malformed/missing post-startup reference behavior, and catastrophic save rollback/participant ordering remain UNKNOWN.
