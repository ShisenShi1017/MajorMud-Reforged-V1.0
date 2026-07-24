# CodeUpdate 0155 — Textblock Invocation Semantics

## Result

The decoded corpus contains 389 `text` operations across 182 contextual quest-stage scripts. Each operation is now modeled as a synchronous typed textblock invocation at its exact source line and sequence.

None of the 389 targets resolves to an executable contextual stage record in the current payload corpus. Runtime therefore fails closed rather than displaying numeric IDs, treating the target as a command, or inventing branch content.

## Ordering and rollback

A target must resolve before nested execution. Nested effects execute at the call position and must finish successfully before the caller continues. Any nested failure aborts the enclosing clause and discards buffered state and output.

## Command boundary

No command opcode exists in the 2,616 decoded operations. The `text` opcode is not a generic command dispatcher. Embedded command behavior remains unproven and is not activated.

## Native dependencies

Closure requires the textblock payload catalog, payload grammar, and any native branch/return rules contained in those payloads.
