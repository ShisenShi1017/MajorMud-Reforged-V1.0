# Remaining Systems Update 0004 — Doors, Locks, Keys, and Access Control

## Executable runtime

Implemented `RuntimeDoorAccessService` as the owner of door lifecycle and access-policy coordination. It validates catalog definitions, creates deterministic state, synchronizes reciprocal door sides, enforces access restrictions, processes open/close/lock/unlock, emits consumable-key requests without stealing Character inventory ownership, schedules resets, persists configured world state atomically, and validates invariants.

Traversal ordering is explicit: destination resolution remains with Rooms; access restrictions are checked; door broken/locked/open state is evaluated; key or ability authorization is processed; trap handling is requested; movement may then proceed.

## Evidence-safe boundaries

Lockpick and bash are represented as evidence-only policy requests. No native chance, durability, damage, or ability formula was invented. Missing definitions, keys, permissions, linked states, or unsupported actions fail closed.

The seven unresolved room latch effects were not reopened and remain assigned to the final native pass.

## Validation

`--smoke-door-access` covers locked traversal, consumable-key requests, synchronized linked doors, open-after-unlock, trap-before-movement ordering, race restriction enforcement, timed reset, reset state, and invariants.

## Remaining native dependencies

Exact Classic strings and aliases, key consumption, lockpick/bash formulas, durability, remote/one-way lock encodings, reset persistence, and the seven room latch effects remain unresolved.
