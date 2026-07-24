# Worldgroup Native Closure Update0027 — MajorMUD Polling Callbacks

Update0027 closes the registration and structural ownership of every callback passed by `wccmmud.dll` to `WGSERVER!_begin_polling`.

## Proven registration map

Twelve direct registration callsites resolve to eight unique callbacks. `0x00402AAA` is reused by four callsites and is the continuously recurrent gameplay heartbeat. `0x00402D12` is reused by two callsites. The remaining six callbacks each back a separate bounded asynchronous or modal workflow.

## Runtime ordering

Worldgroup invokes a polling callback synchronously after consuming the session's `0xC0` polling sentinel. The callback receives no explicit arguments and resolves its current session through host globals. After the callback returns, Worldgroup checks the session polling slot and enqueues exactly one replacement sentinel only when the callback remains registered.

This means a callback's own `_stop_polling` call is the authoritative terminal transition. Seven callbacks contain completion, cancellation, invalid-session, or host-operation-failure stop paths. The gameplay heartbeat intentionally remains registered.

## Gameplay heartbeat

`0x00402AAA` services per-session flags, periodic global maintenance, and mutually exclusive state branches. Its periodic threshold is derived from globals `0x00479640` and `0x00482D80`; its counter is `0x00479650`. No wall-clock unit is assigned because execution cadence depends on host queue service.

## Bounded asynchronous workflows

The other callbacks are structurally classified by their state and storage footprints rather than speculative UI names. They cover retained host record searches, global record iteration, threshold completion, modal validation, and bounded character-state cleanup. Their detailed semantic labels remain intentionally open pending independent string/record provenance.

## Persistent-write boundary

Several callbacks use host DFA/search APIs, but this pass does not infer write, transaction, journal, or recovery semantics from those read/search operations. Persistent DFA write tracing begins from explicit write/dirty/commit imports in the next pass.
