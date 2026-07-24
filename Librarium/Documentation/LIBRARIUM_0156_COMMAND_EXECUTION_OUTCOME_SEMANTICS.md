# CodeUpdate 0156 — Command Execution Outcome Semantics

## Result

All 77 managed runtime commands now have explicit execution-outcome, mutation-ownership, save, failure-event, and rollback-ownership records.

Nonmovement commands publish `received` before dispatch, `completed` after a normal handler return, and `failed` when an uncaught handler exception crosses the dispatcher boundary. Exceptions are returned as player-visible failure text and are never retried implicitly.

## Atomicity

The command dispatcher does not invent a universal rollback mechanism. Atomicity remains owned by the selected domain handler or its persistent transaction. Movement retains its separate completed/blocked contract and updates persisted location only after successful movement.

## Save behavior

Only the explicit `save` command is guaranteed at dispatcher level. Item, economy, quest, recovery, and other mutating commands persist through their owning domain implementations.

## Remaining dependency

Native command handlers not represented in the managed 77-command registry remain tracked for the final native exhaustion pass.
