# Dodge and Evasion

**Mechanic ID:** `COMBAT_DODGE`

**Domain:** Combat

**Status:** open_conflict_leading_nightmare_redux

**Confidence:** blocked_by_open_conflict

**Evidence hits:** 645

**Unique evidence files:** 27

**Runtime status:** do_not_implement_as_classic_verified_yet


## Description

Dodge chance, caps, and placement in attack sequence.


## Source agreement

- NightmareRedux: 189 hits
- MMUDExplorer: 435 hits
- MonsterSim: 21 hits

## Primary buckets

- tooling_ui: 247
- combat: 224
- character_progression: 105
- npc_ai: 62
- data_schema: 6
- world_spawns: 1

## Open conflicts

- `CONFLICT_COMBAT_DODGE_ORDER_001`: Dodge/Evasion order — open_needs_order_confirmation
  - Policy: Nightmare Redux is leading interpretation; confirm sequence against emulator/log behavior before runtime lock.

## Reconstruction notes

- Dodge has enough evidence for raw dodge/caps but remains blocked on exact sequence placement and mode selection.

## Top evidence files

- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` (107 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas` (89 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/frmMonster.frm` (62 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` (58 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/modMain.bas` (46 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/frmMonsterAttackSim.frm` (44 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/frmMonsterAttackSim.frm` (38 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` (38 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/frmPasteChar.frm` (33 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/frmHitCalc.frm` (25 hits)

## Representative evidence

- NightmareRedux `NightmareRedux/Nightmare-Redux-master/README.md` line 16: -UP: update attack sim to allow for true dodge value (as computed in MME v1.9.2) or megamud's value
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 37: Private m_nStatAtkAttemptDodgedOrResisted(0 To 4) As Currency
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 47: Private m_nUserDodge As Long
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 50: Private m_bDodgeBeforeAC As Boolean
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 97: Public Property Let bDodgeBeforeAC(ByVal vData As Boolean)
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 98: m_bDodgeBeforeAC = vData
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 101: Public Property Get bDodgeBeforeAC() As Boolean
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 102: bDodgeBeforeAC = m_bDodgeBeforeAC
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 421: Dim bAttackHit As Boolean, bDodged As Boolean, bGlanced As Boolean, bResisted As Boolean
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 423: Dim nRoll_AttackChance As Integer, nRoll_HitChance As Integer, nRoll_DodgeChance As Integer, nAdjustedDodgeValue As Integer

## Next action

Resolve the conflict before runtime implementation is marked classic-verified. Use Nightmare Redux as leading hypothesis only where source-priority policy applies.