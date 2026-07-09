# CodeUpdate 0108 — Guild / Social Reconstruction (Redo)

Status: **PASS_WITH_LIMITATIONS**

This redo performs a real Guild / Social subsystem reconstruction pass using the uploaded 0107 Quest cumulative baseline plus attached MBBSEmu and MMUD Explorer sources.

## Scope

Covered guild/clan references, party/group behavior, alignment/reputation/faction references, communication commands, player identity/status references, and social/moderation flags.

## Metrics

- Raw guild/social evidence hits: **426,549**
- Files with guild/social hits: **2,375**
- Prioritized evidence references: **1,600**
- Relationship edges: **13,522**
- Guild/social candidate records: **2,348**
- Probes defined: **6**
- Gaps recorded: **8**
- Gameplay mechanics promoted: **0**
- Runtime behaviors replaced: **0**

## Promotion decision

One narrow data-presence record was promoted: `guildsocial.data_presence.guild_party_social_references`. This verifies traceable guild/party/social/communication references only. It does **not** verify guild permissions, party sharing formulas, communication scope, alignment effects, or social command edge cases.

## Category hit summary

- guild: **9,960** hits
- party: **112,364** hits
- alignment: **10,712** hits
- communication: **256,012** hits
- player_social: **37,501** hits

## Remaining guild/social gaps

- Exact guild/clan membership persistence
- Guild rank and permission rules
- Party XP/loot sharing and rounding
- Party lifecycle edge cases
- Communication recipient scope and filtering
- Alignment/reputation/faction thresholds
- Social moderation flags
- Guild hall/world integration

## Next branch

Proceed to **CodeUpdate 0109 — Dialogue & Text Reconstruction** after review, using this cumulative 0108 package as baseline.
