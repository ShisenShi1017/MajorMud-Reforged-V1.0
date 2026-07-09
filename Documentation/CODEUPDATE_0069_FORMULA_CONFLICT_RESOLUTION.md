# CodeUpdate 0069 — Formula Conflict Resolution
Generated UTC: 2026-07-08T19:45:48.310067Z
## Result
- 0068 conflict buckets reviewed: **2**
- True formula conflicts confirmed after manual review: **0**
- Parser/grouping false-positive conflict buckets: **2**
- Runtime formulas replaced: **0**
- Formulas promoted to Verified: **0**

## The two 0068 conflicts

### FC0069-0001 — lhs `text`
- 0068 classification: `potential_conflict_requires_manual_review`
- Sources in bucket: MBBSEmu, MMUD Explorer
- Candidate count reported by 0068: 32
- 0069 resolution: `parser_bucket_false_positive_not_source_disagreement`
- Rationale: The shared lhs "Text" is a VB UI property used for display fields. Entries mix UI assignment, summaries, and candidate formulas. This is not a true cross-source equation conflict until normalized by semantic formula target.
- Representative candidates:
  - `FR0068-c11a72723fb0` — MMUD Explorer `frmMonsterAttackSim.frm:1878` — `txtStatAvgRound(x).Text = Round(clsMonAtkSimThisForm.nStatAtkTotalDamage(x) / clsMonAtkSimThisForm.nStatAtkHits(x))`
  - `FR0068-0e5aab98c2b9` — MMUD Explorer `frmHitCalc.frm:1201` — `txtHitCalc(0).Text = val(txtHitCalc(0).Text) + nGlobalAttackAccyAdj`
  - `FR0068-df86238eff2f` — MMUD Explorer `frmHitCalc.frm:1204` — `txtHitCalc(0).Text = val(txtHitCalc(0).Text) + nGlobalAttackAccyAdj`
  - `FR0068-613117d402eb` — MMUD Explorer `frmHitCalc.frm:1208` — `txtHitCalc(0).Text = val(txtHitCalc(0).Text) + nGlobalAttackAccyAdj`
  - `FR0068-325ce95cac3f` — MMUD Explorer `frmHitCalc.frm:1211` — `txtHitCalc(0).Text = val(txtHitCalc(0).Text) + nGlobalAttackAccyAdj`
  - `FR0068-5cc7e176ffaa` — MMUD Explorer `frmMonsterAttackSim.frm:1874` — `txtStatTrueCast(x).Text = Round(clsMonAtkSimThisForm.nStatAtkAttempted(x) / clsMonAtkSimThisForm.nTotalAttacks, 3) * 100`
  - `FR0068-03cd0884024f` — MMUD Explorer `frmMonsterAttackSim.frm:1884` — `txtStatSuccess(x).Text = Round(clsMonAtkSimThisForm.nStatAtkHits(x) / clsMonAtkSimThisForm.nStatAtkAttempted(x), 3) * 100`
  - `FR0068-3cda967cce3b` — MMUD Explorer `frmMonsterAttackSim.frm:1903` — `'txtStatResistDodge(x).Text = Round(clsMonAtkSimThisForm.nStatAtkAttemptDodgedOrResisted(x) / (clsMonAtkSimThisForm.nStatAtkHits(x) + clsMonAtkSimThisForm.nStatAtkAttemptDodgedOrResisted(x)), 3) * 100`

### FC0069-0002 — lhs `index`
- 0068 classification: `potential_conflict_requires_manual_review`
- Sources in bucket: MBBSEmu, MMUD Explorer
- Candidate count reported by 0068: 6
- 0069 resolution: `parser_bucket_false_positive_not_source_disagreement`
- Rationale: The shared lhs "Index" is a control/event parameter or local variable, not a gameplay formula output. The bucket mixes MBBSEmu memory allocator test code with MMUD Explorer UI event branches.
- Representative candidates:
  - `FR0068-fc4b2d9624bd` — MBBSEmu `MBBSEmu.Tests/Memory/MemoryAllocator_Tests.cs:212` — `var index = random.Next(allocatedMemory.Count);`
  - `FR0068-20402da81142` — MMUD Explorer `frmBSCalc.frm:700` — `If Index = 0 Then 'minus LEVEL`
  - `FR0068-1d82ed802091` — MMUD Explorer `frmBSCalc.frm:801` — `'    If Index = 0 Then 'minus LEVEL`
  - `FR0068-d324445816cc` — MMUD Explorer `frmMain.frm:19463` — `If Index = 1 Then 'attack type`
  - `FR0068-34adf3a33b64` — MMUD Explorer `frmMain.frm:22019` — `ElseIf Index = 3 Or Index = 4 Then 'equip / spellbook`
  - `FR0068-18e21251279c` — MMUD Explorer `frmMain.frm:23182` — `ElseIf Index = 6 Then 'choose attack`

## Why none were promoted
- MMUD Explorer and MBBSEmu are external evidence-only sources under the Librarium-first rule.
- The two reported 0068 conflicts were not exact formula disagreements after manual review; they were broad parser buckets based on generic lhs names (Text and Index).
- No candidate was tied to independent Classic runtime observation or reference-pack proof of the exact equation, rounding, timing, and edge-case behavior.
- Candidate lines remain useful as Derived evidence and gap targets, but not sufficient for Verified promotion.

## Interpretation
The 0068 conflict detector grouped candidates by raw left-hand-side token. That is useful for triage, but too broad for promotion decisions. `Text` is commonly a VB display/UI property, and `Index` is commonly a UI event parameter or local variable. These buckets do not prove that MMUD Explorer and MBBSEmu disagree about a Classic MajorMUD gameplay equation.

## Next work
- Normalize candidates into semantic formula targets instead of raw lhs buckets.
- Run simulator/control scenarios for high-impact formulas: hit/defense, damage, regen, XP, spawn timing.
- Corroborate exact equation and rounding behavior with Reference Pack or observed Classic behavior.
- Only then promote Derived candidates to Verified.
