# DataDecode0101 — Relationship & Dependency Decision Pass

Status: complete.

This pass is cumulative over DataDecode0100 and remains research-only. `/Data` was read from the extracted repository archive only and was not modified. No runtime validation was performed.

## Work completed

- Finalized a static RelationshipDecisionMatrix from observed canonical JSON references.
- Registered candidate foreign keys where all observed references resolve.
- Registered lookup ownership decisions by referenced identifier owner.
- Generated a dependency graph and static candidate load order.
- Generated an unresolved dependency register for absent or incomplete targets.
- Updated evidence, decision, confidence, metrics, and source-artifact verification files.

## Data Confidence Summary

- C100: 0 relationship decisions.
- C95: 4 relationship decisions with all observed canonical references resolved.
- C85: 3 native source-key preservation decisions.
- C70: 2 review-only or unresolved relationship decisions.

## Promotions

None. No confidence was promoted beyond the available static evidence.

## Demotions

None.

## What changed

DataDecode0101 adds relationship and dependency decision artifacts without adding records, fields, schemas, generated data, imports, or runtime output.

## What remains unknown

Behavioral semantics, unresolved target classes such as NPCs where present only as room references, and any native source-key interpretation that requires deeper binary/static corroboration remain unknown. Runtime validation is still deferred.
