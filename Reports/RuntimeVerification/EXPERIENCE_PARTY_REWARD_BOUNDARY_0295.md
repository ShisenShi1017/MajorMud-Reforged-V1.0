# Experience / Party Reward Boundary Verification 0295

Pass 24 verifies that race/class EXP fields are only consumed by progression-threshold calculation and cannot scale earned XP awards. It also verifies the STRONG six-character Classic party cap and preserves the explicit no-auto-party-loot boundary.

Expected invariants:

- no `ApplyDecodedExperienceModifier` award path exists
- `ExperienceChartPercent` owns race/class EXP chart combination
- raw `AwardExperience` persists the earned amount unchanged
- Classic party creation uses `RuntimePartyPolicy.ClassicStrongEvidence`
- Classic strong policy has `MaximumMembers: 6`
- invite lifetime remains unproven/null
- XP remainder remains unresolved
- no automatic party loot distribution is introduced
