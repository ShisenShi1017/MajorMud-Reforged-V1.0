# NPC Attack Energy Loop

**Mechanic ID:** `NPC_ATTACK_LOOP`

**Domain:** NPC AI / Combat

**Status:** verified_high_confidence

**Confidence:** high

**Evidence hits:** 557

**Unique evidence files:** 36

**Runtime status:** ready_for_test_mapping_before_runtime_sync


## Description

NPC action-energy loop controlling physical attacks and repeated spell attempts.


## Source agreement

- NightmareRedux: 206 hits
- MMUDExplorer: 307 hits
- MonsterSim: 44 hits

## Primary buckets

- combat: 254
- tooling_ui: 191
- character_progression: 58
- npc_ai: 30
- items_equipment: 12
- data_schema: 6
- world_spawns: 6

## Top evidence files

- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/frmSwingCalc.frm` (68 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm` (58 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` (56 hits)
- MonsterSim: `MonsterSim/MajorMUD-PHP-Monster-Sim-master/monster.php` (42 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/modMain.bas` (40 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas` (39 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` (35 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` (35 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/frmPasteChar.frm` (30 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/frmMonster.frm` (30 hits)

## Representative evidence

- NightmareRedux `NightmareRedux/Nightmare-Redux-master/Nightmare Redux.vbp` line 62: Form=frmSwingCalc.frm
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/Nightmare Redux.vbw` line 50: frmSwingCalc = 338, 338, 2136, 1083, C, 208, 208, 1372, 1054, C
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/README.md` line 215: -UP: updated the swing calculator
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/README.md` line 271: -NEW: swing calculator tool
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 287: Dim nLeastEnergyUsed As Long, nLowestCostAttack As Long, nLowEnergy As Long
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 288: Dim nMaxEnergyRound As Long, nMaxCostAttack As Long
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 290: Dim nEnergyForAttack As Long, nEnergyRemaining As Long, nDamagePerEnergy As Currency
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 309: If m_nAtkType(nAttack) > 0 And m_nAtkEnergy(nAttack) > 0 Then
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 310: nLowEnergy = m_nAtkEnergy(nAttack)
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 315: nLowEnergy = Round(m_nAtkEnergy(nAttack) / 2)

## Next action

Convert this record into deterministic tests and runtime/simulator mapping.