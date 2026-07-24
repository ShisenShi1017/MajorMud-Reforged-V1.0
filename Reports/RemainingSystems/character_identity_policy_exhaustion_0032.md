# Remaining Systems 0032 — Character Identity Policy

Implemented executable Reforged coordination for canonical name validation, reserved and duplicate-name rejection, owner-scoped titles, visibility filtering, guild-tag projection, disguise presentation, and hidden identities. Character remains authoritative for stored identity; this service owns validation and presentation policy only.

## Evidence boundary

The implementation is a safe compatibility shell. Exact MajorMUD 1.11p limits, reserved names, title tables, precedence, disguise behavior, rendering, and Worldgroup ownership remain final-native-pass dependencies. Unknown rules fail closed and are not inferred.

## Validation

`--smoke-character-identity-policy` covers valid, reserved, prefixed, malformed, and duplicate names; disguise and hidden presentation; administrative visibility; owner-scoped title grants; and catalog invariants.
