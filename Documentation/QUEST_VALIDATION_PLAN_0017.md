# Quest Validation Plan

MajorMUD quest state appears tied to ability flags. The runtime now has an Ability Engine and Quest Engine, but canonical quest data is not yet promoted.

## Required approach
- Treat abilities/flags as the primitive state layer.
- Build quest definitions only when supported by evidence.
- Record provenance for every mapping.
- Keep synthetic test quests out of `/Data`.

## Outputs to create next
- Ability flag inventory
- Quest evidence index
- NPC/dialogue/shop/travel requirement crosswalk
- Quest flag crosswalk
- Promoted quest definitions only when evidence-backed
