# Magic Mana and Cast Pipeline

**Mechanic ID:** `MAGIC_MANA_CAST`

**Domain:** Magic

**Status:** probable_needs_reconstruction

**Confidence:** medium_reconstruction_needed

**Evidence hits:** 2011

**Unique evidence files:** 93

**Runtime status:** requires_formula_record_expansion_before_runtime_sync


## Description

Spell costs, cast requirements, spell effect dispatch, and cast failure handling.


## Source agreement

- NightmareRedux: 518 hits
- MBBSEmu: 163 hits
- MMUDExplorer: 1314 hits
- MonsterSim: 16 hits

## Primary buckets

- combat: 725
- tooling_ui: 629
- character_progression: 280
- npc_ai: 95
- data_schema: 72
- networking_runtime: 57
- magic: 34
- world_spawns: 27
- commands_client: 26
- quests_scripts: 20

## Top evidence files

- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` (358 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/modExpPerHour.bas` (266 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/modMain.bas` (231 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas` (183 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/frmMonster.frm` (95 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/frmMonsterAttackSim.frm` (65 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/frmPopUpOptions.frm` (60 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/frmUser.frm` (56 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/modMMudDatabase.bas` (43 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` (43 hits)

## Representative evidence

- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 83: 2688|0|0|(this is just the last part) dao lords death spell casts another spell which executes this block.  it has the flag checks, exp gain, and giving the item.
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 755: --Nahr's Castle
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 765: 4305|0|0|caster nahr portal in
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 766: 4301|0|0|caster nahr portal out
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 820: 4244|0|4269|spellcaster filter, checks casting stat
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 824: 4271|0|4275|summon fire shard, checks casting stat
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 825: 4272|0|4275|summon light shard, checks casting stat
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 826: 4273|0|4275|summon ice shard, checks casting stat
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 827: 4274|0|4275|summon earth shard, checks casting stat
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 838: 4299|0|4324|gather ore checks class, casting stat

## Next action

Expand the formulas and data-field mapping for this mechanic using the evidence records above.