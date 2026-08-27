# Code Update 0295 — Experience Charts / Party Reward Boundary (Pass 24)

## Scope

Pass 24 corrects the remaining semantic leak between earned XP and race/class progression cost, then narrows the Classic party reward/capacity boundary without inventing unresolved leader, rank, disconnect, or XP-remainder behavior.

## Race/class EXP semantics

Race and class EXP values are **experience-chart inputs**. They determine how much total experience the character must accumulate to train to each level. They do not modify the amount of experience awarded by a monster, quest, textblock, or other reward.

The retained stock calculator remains:

- class chart = `class ExpTable + 100`
- race ExpTable is added to the class chart
- the combined chart feeds the recovered level-threshold formula

`RuntimeCharacterProgressionResolver.AwardExperience` now owns raw earned XP only. The obsolete overload that multiplied an award by the race/class chart was removed.

## Party capacity

Native-era community/reverse-engineering evidence consistently identifies the MajorMUD party limit as **six total characters**. Classic party creation now uses a STRONG-evidence six-member policy. The exact native full-party failure message remains UNKNOWN.

## XP and loot party boundary

NPC XP is not party-distributed. Eligibility is the set of characters actively fighting that exact NPC at the moment it dies. Each observed recipient receives the integer quotient. Nonzero remainder disposition remains UNKNOWN.

Native-era captures also show monster currency dropping onto the room floor. Reforged therefore does not auto-split NPC loot by party membership. `SHARE` remains the explicit command for voluntary party/room currency distribution.

## Score

No requirement changes classification in this pass. The active score remains **73 PASS / 75 STRONG / 31 UNKNOWN = 179**. The change corrects an already-PASS requirement's meaning and narrows the still-UNKNOWN party bucket.
