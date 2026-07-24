# Worldgroup Native Closure Update0026 — Polling Sentinel and Queue Cadence Correction

## Corrected host boundary

Update0025 correctly recovered callback storage, invocation, self-stop, and re-registration behavior, but incorrectly described the value `0xC0` as a raw delay. Direct import resolution proves `0x0044C264` is the WGSERVER thunk for `GALGSBL!_btuinj`. The function injects one byte into a session input queue; it is not a timer service.

## Proven cooperative polling cycle

`_begin_polling(session, callback)` stores the callback at session offset `+0x48`. When the slot was previously empty and the same session is not already executing, WGSERVER injects byte `0xC0` into that session. The input/event selectors route `0xC0` to `0x004231CF`, which invokes the registered callback with no explicit arguments. After the callback returns, WGSERVER injects one new `0xC0` byte only if the callback pointer remains nonzero. Calling `_stop_polling` from inside the callback therefore prevents recurrence.

This is cooperative queue-driven polling. `0xC0` has no duration and cannot be converted to milliseconds, seconds, rounds, or ticks from this path. Actual cadence depends on how quickly the host services the session input/event queue.

## GALGSBL queue implementation

`GALGSBL!_btuinj` validates the session, requires the session-active flag, advances a ring index modulo `0x200`, and stores the injected byte in the session queue at offset `+0x329`. The ring uses read/write indices at `+0x58` and `+0x5C`. On collision/full behavior, the routine writes `0xFE` to the current queue slot.

## Task scheduler status

The Update0025 `_prctask` round-robin semantics remain valid. Its exact caller cadence remains bounded: no direct WGSERVER rel32 callsite and no static PE import of `_prctask` was found in the supplied installation, indicating an indirect or dynamically registered invocation path.
