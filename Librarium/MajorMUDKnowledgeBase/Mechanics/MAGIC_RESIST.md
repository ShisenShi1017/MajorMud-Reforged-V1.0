# Magic Resistance

**Mechanic ID:** `MAGIC_RESIST`

**Domain:** Magic

**Status:** verified_high_confidence

**Confidence:** high

**Evidence hits:** 730

**Unique evidence files:** 31

**Runtime status:** ready_for_test_mapping_before_runtime_sync


## Description

Magic resistance check and spell damage reduction behavior.


## Source agreement

- NightmareRedux: 275 hits
- MMUDExplorer: 420 hits
- MonsterSim: 35 hits

## Primary buckets

- combat: 330
- tooling_ui: 228
- character_progression: 143
- magic: 11
- data_schema: 7
- items_equipment: 5
- npc_ai: 3
- world_spawns: 3

## Top evidence files

- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` (117 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas` (103 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` (97 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` (64 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/frmMonsterAttackSim.frm` (56 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` (53 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/modMain.bas` (45 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/frmMonsterAttackSim.frm` (41 hits)
- MonsterSim: `MonsterSim/MajorMUD-PHP-Monster-Sim-master/monster.php` (32 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/modMMudDatabase.bas` (20 hits)

## Representative evidence

- NightmareRedux `NightmareRedux/Nightmare-Redux-master/README.md` line 47: -UP: Changed drop down spell editor's "Type of Resist" to what I believe they actually mean
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/README.md` line 52: -UP: Added spell resist type (if the spell is completely resistible)
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/README.md` line 164: -UP: added boxes to show spell damage vs. X MR for damage spells
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/README.md` line 314: -NEW: "Charm Resistance" field for monsters added (thx Tsunami)
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 30: Private m_nAtkResist(0 To 4) As Integer
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 31: Private m_nAtkMRdmgResist(0 To 4) As Integer
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 36: Private m_nStatAtkDmgResisted(0 To 4) As Currency
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 40: Private m_nStatHitSpell_AtkDmgResisted(0 To 4) As Currency
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 42: Private m_nStatBetweenRound_AtkDmgResisted(0 To 4) As Currency
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 83: Private m_nBetweenRoundResistType(0 To 4) As Integer

## Next action

Convert this record into deterministic tests and runtime/simulator mapping.