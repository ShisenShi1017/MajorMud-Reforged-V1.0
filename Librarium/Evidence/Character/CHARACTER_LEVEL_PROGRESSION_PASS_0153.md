# Character Level Progression — Pass 0153

## Promoted runtime behavior

**STRONGLY CORRELATED:** Trainer promotion now consumes XP and CP from one canonical progression schedule row. A row is executable only when it carries `proven` or `strongly_correlated` classification and explicit source provenance. Class/race applicability is resolved canonically, equivalent duplicates are tolerated, conflicting duplicates are rejected, and XP thresholds must increase relative to the nearest applicable prior level. Evidence-backed level caps are enforced only when the row supplies one.

This removes the prior broad recursive scan that could accidentally combine an XP value from one unrelated JSON object with a CP value from another. Promotion remains atomic and fail-closed.

## Generator result

The 0153 generator exhaustively scans Classic JSON for level/XP-shaped candidates and preserves them as WEAK, non-executable evidence. It does not promote absolute rows without source classification.

## Preserved unknowns

Absolute native XP/CP rows, SP/EP schedules, skill/spell/ability grants, multiclass switching, global caps, and post-cap overflow remain UNKNOWN and blocked.
