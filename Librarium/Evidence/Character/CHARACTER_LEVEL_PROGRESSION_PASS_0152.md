# Character Level Progression — Pass 0152

## Promoted runtime behavior

**PROVEN:** WCCCLASS exposes `min_hits`, `max_hits_bonus`, magery type, and magery level. WCCRACE exposes the per-level HP adjustment. The reconstructed native secondary-stat formula identifies the level-up HP roll as an integer from zero through the class bonus cap and shows that constitution adjustment participates twice.

Promotion now resolves one HP roll, computes the exact HP delta from class minimum, race adjustment, roll, and the change in constitution adjustment, and applies it atomically with level and CP. Mana growth follows the already promoted native formulas: zero for non-casters, `magery_level * 2` for types 1–4, and one for type 5. Current HP and mana rise by the positive maximum increase without exceeding the new maximum.

The promotion audit marker preserves active class, target level, HP roll, HP delta, mana delta, and source record. Checked arithmetic blocks overflow before persistence.

## Preserved unknowns

Absolute XP rows, SP/EP schedules, skill/spell/ability grants, native multiclass switching, level caps, and post-cap overflow remain UNKNOWN. Runtime continues to block promotions lacking executable XP or CP evidence and does not invent any missing schedule.

## Impacts

- Runtime: evidence-backed HP/mana growth is active.
- Save: existing fields and markers; no save-version change.
- World state: character-only atomic update.
