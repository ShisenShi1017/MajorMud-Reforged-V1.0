# Worldgroup Native Closure Update0006 — Hook Vector Registration and Invocation

## Proven corrections

- `0x004381F2` is exported as `_add2arr`; it is a generic growable-array insertion routine, not a callback dispatcher.
- The lifecycle `_hook_*` exports register one raw four-byte callback pointer into event-specific arrays.
- Each event array is created with initial/growth capacity 25 and element size 4.

## Proven runtime semantics

- `_newarr` creates a 20-byte descriptor: data pointer, capacity, count, growth increment, and element size.
- `_add2arr` expands storage, copies or zero-fills one element, increments count, and returns the inserted slot.
- `_dovecs` snapshots the current count, iterates index 0 upward, and invokes every callback in FIFO registration order.
- Callback return values are ignored; there is no stop/continue branch in `_dovecs`.
- Callbacks registered during an active `_dovecs` pass are deferred because the count is captured before iteration.
- Shutdown invokes the shutdown vector before the final-shutdown vector.

## Runtime impact

The host lifecycle boundary can now be implemented as deterministic ordered callback vectors rather than an assumed linked-list dispatcher. Update0005 evidence is preserved but its dispatcher interpretation is explicitly superseded.
