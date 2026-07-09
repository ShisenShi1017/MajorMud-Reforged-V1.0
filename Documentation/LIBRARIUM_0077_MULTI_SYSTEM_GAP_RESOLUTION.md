# CodeUpdate-0077 Multi-System Formula Gap Resolution Report
Generated: 2026-07-08T20:23:37.784251+00:00
## Scope
This update resolves multiple high-impact formula behavior gaps in one work package: combat hit/damage, backstab/crit/special attacks, XP/leveling, regen/ticks, loot, and spawn timing. The Librarium remains canonical; MMUD Explorer and MBBSEmu remain evidence-only.
## Summary
- **baseline_loaded**: True
- **tracks_created**: 5
- **candidate_references_selected**: 400
- **gap_records_updated**: 28
- **probe_definitions_created**: 15
- **verified_promotions**: 0
- **runtime_formula_replacements**: 0
- **librarium_canonical_preserved**: True
- **external_sources_evidence_only**: True

## Resolution Track Results
### 0077-COMBAT-HIT-DAMAGE — Combat hit and damage math
- Candidate pool: **1032**
- Prioritized candidate references: **80**
- Source counts: `{'MMUD Explorer': 986, 'MBBSEmu': 46}`
- Result: **partial_behavior_shape_only**
- Promotion: **no**
- Runtime replacement: **no**
- Representative candidates:
  - `FR0068-9bc7b63e7529` — MMUD Explorer `README.md:19` — -FIX: Weapon +hitmagic was not being considered in vs mob/lair damage output calculations for regular melee attacks
  - `FR0068-eb7b577fa3b8` — MMUD Explorer `clsMonsterAttackSim.cls:792` — nHitSpellDamage = Round(Me.RandomNumber(m_nAtkHitSpellMin(nAttack), m_nAtkHitSpellMax(nAttack)))
  - `FR0068-fc5daf775313` — MMUD Explorer `clsMonsterAttackSim.cls:796` — nMR_Reduction = Round(CalcResistedDamage(nHitSpellDamage, m_nUserMR, m_nUserAntiMagic))
  - `FR0068-edc5ada47410` — MMUD Explorer `clsMonsterAttackSim.cls:815` — nHitSpellDamage = Round(nHitSpellDamage - nResist_Reduction)
  - `FR0068-6563a276fa07` — MMUD Explorer `clsMonsterAttackSim.cls:868` — nRoundDamage = nRoundDamage + nDamage + nHitSpellDamage

### 0077-BACKSTAB-CRIT-SPECIAL — Backstab, critical, and special attack behavior
- Candidate pool: **870**
- Prioritized candidate references: **80**
- Source counts: `{'MMUD Explorer': 839, 'MBBSEmu': 31}`
- Result: **partial_behavior_shape_only**
- Promotion: **no**
- Runtime replacement: **no**
- Representative candidates:
  - `FR0068-840341edc7ef` — MMUD Explorer `README.md:78` — -FIX: Surprise punch showing on weapon tab when hitmagic filter applied and filter criteria not met
  - `FR0068-45c21e39c8fb` — MMUD Explorer `README.md:187` — -NEW: Critical Hit Chance formula discovered and now calculated automatically
  - `FR0068-292843b686ff` — MMUD Explorer `docs/changelog.archive.txt:93` — -NEW: Critical Hit Chance formula discovered and now calculated automatically
  - `FR0068-e8cfce60c263` — MMUD Explorer `docs/changelog.txt:75` — -FIX: Surprise punch showing on weapon tab when hitmagic filter applied and filter criteria not met
  - `FR0068-9700688848b6` — MMUD Explorer `docs/changelog.txt:175` — -NEW: Critical Hit Chance formula discovered and now calculated automatically

### 0077-XP-LEVEL — Experience and level progression
- Candidate pool: **556**
- Prioritized candidate references: **80**
- Source counts: `{'MMUD Explorer': 415, 'MBBSEmu': 141}`
- Result: **partial_behavior_shape_only**
- Promotion: **no**
- Runtime replacement: **no**
- Representative candidates:
  - `FR0068-7f847055594f` — MMUD Explorer `frmMain.frm:35136` — sClipBoardText = sClipBoardText & "Experience:" & (CalcExpNeededByRaceClass(nLevel, nClass, nRace) + 1) & vbCrLf
  - `FR0068-e36d3a5984ac` — MBBSEmu `CLAUDE.md:98` — - `AllowUnsafeBlocks` is enabled — unsafe code is expected for low-level memory/CPU emulation.
  - `FR0068-b7429a203ffe` — MBBSEmu `MBBSEmu.Tests/ExportedModules/Majorbbs/prf_Tests.cs:26` — [InlineData("Level: %-5d", "Level: 3    ", (ushort)3)]
  - `FR0068-29be92480931` — MBBSEmu `MBBSEmu.Tests/ExportedModules/Majorbbs/prf_Tests.cs:28` — [InlineData("Level: %-5.5d", "Level: 00003", (ushort)3)]
  - `FR0068-aaab8da91626` — MBBSEmu `MBBSEmu.Tests/ExportedModules/Majorbbs/sprintf_Tests.cs:26` — [InlineData("Level: %-5d", "Level: 3    ", (ushort)3)]

### 0077-REGEN-TICKS — Regeneration and tick timing
- Candidate pool: **572**
- Prioritized candidate references: **80**
- Source counts: `{'MMUD Explorer': 548, 'MBBSEmu': 24}`
- Result: **partial_behavior_shape_only**
- Promotion: **no**
- Runtime replacement: **no**
- Representative candidates:
  - `FR0068-dff7910ab287` — MMUD Explorer `docs/exp-per-hour-models.md:15` — | D (`ceph_ModelD`) | **Round-by-round simulation** in real game ticks. Uses the **canonical `nRTK`** (from `CalcCombatRounds` via `GetLairInfo`, same input A/B
  - `FR0068-bf1d4e4033ca` — MMUD Explorer `docs/exp-per-hour-models.md:52` — - Tick constants: round 5s, rest 20s, regen 30s, meditate 10s. Mob HP-regen
  - `FR0068-97c942bd66dc` — MMUD Explorer `modExpPerHour.bas:292` — 'Per-mob HP regen per tick and per round
  - `FR0068-29ea5a5a1d1a` — MMUD Explorer `modExpPerHour.bas:297` — regenPerRound = perMobRegenTick * (SEC_PER_ROUND / (GMUD_MOB_HPREGEN_ROUNDS * SEC_PER_ROUND))
  - `FR0068-d8097b258bfe` — MMUD Explorer `modExpPerHour.bas:299` — regenPerRound = perMobRegenTick * (SEC_PER_ROUND / (STOCK_MOB_HPREGEN_ROUNDS * SEC_PER_ROUND))

### 0077-LOOT-SPAWN — Loot and spawn timing
- Candidate pool: **94**
- Prioritized candidate references: **80**
- Source counts: `{'MMUD Explorer': 89, 'MBBSEmu': 5}`
- Result: **partial_behavior_shape_only**
- Promotion: **no**
- Runtime replacement: **no**
- Representative candidates:
  - `FR0068-abe488a1f4f1` — MMUD Explorer `README.md:73` — -UP: NoFirstKillDrop ability will now show the item name in [GreaterMUD]
  - `FR0068-b4e144cfc068` — MMUD Explorer `README.md:155` — -UP: Added a filter option to the bless spell dropdowns
  - `FR0068-c3931ac375d5` — MMUD Explorer `README.md:273` — -UP: lairs now show minutes to respawn (note that it's /usually/ what is displayed +1 minute due to timing)
  - `FR0068-a0ac56b8bcfc` — MMUD Explorer `README.md:403` — -UP: increased magic level dropdown on weapons from 6+ to 10+
  - `FR0068-14af49fa4bff` — MMUD Explorer `README.md:556` — -UP: expanded width of bless spell dropdown

## Why no formulas were promoted
Promotion requires proof of exact Classic behavior, not just a plausible source-code formula. The evidence selected here supports targeted probe design and narrows candidate inputs/consumers, but it still does not prove exact equations, RNG bounds, rounding, timing, ordering, or edge cases. Therefore all affected mechanics remain Derived/partially mapped and no runtime formulas were replaced.
