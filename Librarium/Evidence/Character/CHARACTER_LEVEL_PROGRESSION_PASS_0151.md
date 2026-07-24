# Character Level Progression Pass 0151

## Promoted runtime behavior

- Character experience is persisted as a total and as an explicit active-class ledger.
- Existing saves normalize deterministically into the progression ledger without changing totals or levels.
- NPC experience awards update both ledgers in one checked transaction.
- Arithmetic overflow rejects the award and leaves the save unchanged.
- Trainer promotion advances exactly one level, updates the active-class level, awards decoded CP, and saves atomically.
- Class and race experience percentage modifiers remain Librarium-authoritative decoded data.

## Evidence classification

**PROVEN:** WCCCLASS/WCCRACE decoded percentage modifiers, existing generated trainer restrictions, character total XP, class-keyed persistence ownership, and checked transaction ordering.

**UNKNOWN:** the absolute native XP table, HP/SP/EP growth schedule, skill/spell/ability gain schedules, native multiclass switching, level caps, and post-cap overflow. Runtime blocks missing promotion data rather than inventing values.

## Persistence

Save version 7 owns class levels, class experience, active class, last XP award provenance, and last promotion provenance. Versions 1–6 remain loadable and normalize on the next save.
