# Code Update 0290 — Borin Clean Build Warning Repair

## Proven warning

`BorinPlayerSessionLifecycle.PruneRawGameplayJournalsAsync` was declared `async`
without any `await` expression, producing compiler warning CS1998.

The same source warning appears twice during a WPF build because the generated
temporary WPF project and the final application project both compile the file.

## Repair

The method retains its existing `Task<int>` public API so all current callers
remain unchanged, but the unnecessary `async` modifier is removed.

Immediate return paths now use `Task.FromResult(0)` and the final result uses
`Task.FromResult(deleted)`.

## Runtime effect

No retention logic, file-deletion behavior, cancellation behavior, or caller
contract changes. This only removes the unnecessary asynchronous state machine
and CS1998 warning.
