# Character Abilities Pass 0146

## Promoted behavior

Race and class `ability_slots` are permanent character-creation grants. Race grants are evaluated first in source order, followed by class grants in source order. Duplicate ability rows are preserved because repeated rows and differing values are authoritative source data, not accidental duplicates.

Every referenced ability ID resolves against `Data/Classic/abilities.json`. Ownership is race-creation or class-creation; these grants are saved and do not reset.

## Runtime correction

`RuntimeAbilityCatalog` previously loaded `skills.json`. It now loads the canonical `abilities.json` family.

## Blocked

The numeric value field is retained exactly but is not assigned one global meaning. Passive, activated, temporary, learned, trainer, cooldown, and removal semantics remain ability-specific and require later evidence.
