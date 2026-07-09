# Combat Hit vs AC

**Mechanic ID:** `COMBAT_HIT_AC`

**Domain:** Combat

**Status:** verified_high_confidence

**Confidence:** high

**Evidence hits:** 419

**Unique evidence files:** 69

**Runtime status:** ready_for_test_mapping_before_runtime_sync


## Description

To-hit resolution against armor class for physical attacks.


## Source agreement

- NightmareRedux: 222 hits
- MBBSEmu: 23 hits
- MMUDExplorer: 167 hits
- MonsterSim: 7 hits

## Primary buckets

- combat: 102
- tooling_ui: 93
- npc_ai: 65
- character_progression: 62
- items_equipment: 33
- data_schema: 17
- quests_scripts: 15
- logging_debug: 12
- commands_client: 6
- networking_runtime: 5

## Top evidence files

- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/frmMonster.frm` (65 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` (53 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/frmItem.frm` (19 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas` (16 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/modMain.bas` (15 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/modFieldmaps_vO.bas` (14 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/modFieldmaps_vN.bas` (13 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/modMMudDatabase.bas` (12 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/modFieldmaps.bas` (12 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` (11 hits)

## Representative evidence

- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 143: 318|0|325|MISSION command given to wounded messenger
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 182: 518|0|495|MISSIONS command given to annora
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 205: 518|0|495|MISSIONS command
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 218: 1112|0|1113|PERMISSION command
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 273: 518|0|495|MISSIONS command given to annora
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 288: 518|0|495|missions command
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 349: 505|0|506|MISSIONS command given to grey lord
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 380: 505|0|506|MISSIONS command given to grey lord
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 399: 505|0|506|MISSIONS command
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 441: 505|0|506|MISSIONS command given to grey lord

## Next action

Convert this record into deterministic tests and runtime/simulator mapping.