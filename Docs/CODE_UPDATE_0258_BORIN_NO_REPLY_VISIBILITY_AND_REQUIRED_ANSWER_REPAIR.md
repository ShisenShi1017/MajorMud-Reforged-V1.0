# Code Update 0258 - Borin NO_REPLY Visibility and Required-Answer Repair

## Purpose

`[NO_REPLY]` is an internal control signal used when Borin should yield a public conversation floor or cancel an optional natural-initiative line. It is not character dialogue and must never appear in Forge or be transmitted through `SAY` or `TELEPATH`.

## Changes

- Added a shared deterministic recognizer for bracketed, unbracketed, spaced, underscored, hyphenated, role-prefixed, and structured speech variants.
- Required-answer turns treat an unexpected silence marker as empty output and retry once with compact context.
- Private Forge turns explicitly prohibit silence markers.
- Directly addressed external turns explicitly prohibit silence markers after the deterministic group gate approves the reply.
- Forge display insertion suppresses any remaining marker as a final defense.
- Private Forge receives an honest deterministic fallback if both model attempts fail to produce a usable answer.
- Optional natural initiative and explicit group-yield turns retain true silence behavior without exposing the marker.
- Added deterministic regression coverage ensuring control markers never become speech while ordinary prose remains intact.

## Boundaries

This update does not modify AI models, configuration, credentials, knowledge, memories, journals, gameplay permissions, or the 270-character in-game limit.
