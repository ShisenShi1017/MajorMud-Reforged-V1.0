# Worldgroup Native Closure Update0025 — Scheduling and Polling Runtime

## Proven session polling contract

`_begin_polling(session, callback)` stores exactly one callback pointer in the Worldgroup session record at offset `+0x48`. If the slot was previously empty and that session is not the one currently executing, Worldgroup requests an initial delay using the raw value `0xC0`. Calling `_begin_polling` again replaces the callback pointer. `_stop_polling(session)` clears the slot.

The host invokes the callback indirectly at `0x004231EC` with no explicit stack arguments. It sets global `0x00451660` to the active session index during the callback and restores it to `-1` afterward. If the callback remains registered after returning, Worldgroup requests another raw `0xC0` delay. A callback can therefore stop its own recurrence by calling `_stop_polling`.

## Proven task scheduler contract

`_initask(callback)` owns a growable vector of 4-byte callback pointers, initially five entries and growing by five. It reuses the first null slot or appends and returns the zero-based index. `_mfytask(index, callback)` replaces a slot.

`_prctask()` advances a persistent cursor modulo the vector count, skips null slots, and invokes at most one non-null callback per call. The callback receives its zero-based task index as its single stack argument. This establishes fair round-robin ordering rather than draining every registered task in one host cycle.

## MajorMUD binding

The supplied WCCMMUD module contains 12 calls to `_begin_polling` and 9 calls to `_stop_polling`. Proven registered callback addresses include the login/name flow, gameplay user polling, automatic update polling, refresh-items polling, user cross-reference polling, and user-item cross-reference polling.

## Bounded unknowns

The raw `0xC0` delay unit and the exact host cadence that calls `_prctask` remain open. No conversion to seconds or game rounds is activated without further host evidence.
