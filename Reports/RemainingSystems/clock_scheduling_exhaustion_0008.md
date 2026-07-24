# Clock, Timer, Calendar, and Scheduling Exhaustion — Update 0008

## Implemented

`RuntimeClockSchedulingService` now owns validated one-shot, fixed-interval, and daily UTC definitions; deterministic dispatch ordering; enable/disable and rescheduling; bounded missed-timer recovery; clock-rollback rejection; schema-versioned world-state persistence; and restart restoration.

Dispatches identify the owning subsystem and action but do not execute foreign state mutations. Reset, event, merchant, trainer, spawn, quest, status, and transport systems retain action ownership.

## Determinism

Due schedules are ordered by due time, then canonical schedule ID, and receive a monotonic dispatch sequence. Catch-up is bounded per definition. Backward clock movement fails closed.

## Validation

`--smoke-clock-scheduling` covers simultaneous ordering, skip-to-next, bounded catch-up, one-shot completion, disable behavior, state invariants, and clock rollback.

## Native dependencies

Exact MajorMUD round/tick conversion, host-time versus UTC behavior, restart catch-up, drift, overflow, simultaneous ordering, calendar rollovers, daylight-saving coupling, and source schedules remain assigned to the final native pass.
