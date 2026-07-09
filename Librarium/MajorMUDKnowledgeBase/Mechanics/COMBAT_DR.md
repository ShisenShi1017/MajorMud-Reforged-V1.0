# Physical Damage Reduction

**Mechanic ID:** `COMBAT_DR`

**Domain:** Combat

**Status:** open_conflict_leading_nightmare_redux

**Confidence:** blocked_by_open_conflict

**Evidence hits:** 139

**Unique evidence files:** 30

**Runtime status:** do_not_implement_as_classic_verified_yet


## Description

How physical damage reduction is applied to successful physical damage.


## Source agreement

- NightmareRedux: 60 hits
- MMUDExplorer: 78 hits
- MonsterSim: 1 hits

## Primary buckets

- tooling_ui: 48
- combat: 45
- items_equipment: 21
- character_progression: 15
- data_schema: 5
- npc_ai: 3
- world_spawns: 2

## Open conflicts

- `CONFLICT_COMBAT_DR_001`: Physical Damage Reduction — open_leading_nightmare_redux
  - Policy: Prefer Nightmare Redux 2025 over historical Monster Sim when unsupported by original observations; seek MBBSEmu/runtime corroboration before classic-verified.

## Reconstruction notes

- Policy: because Nightmare Redux is current and MonsterSim is historical, prefer Nightmare Redux as the working hypothesis, but keep classic-verified blocked until an original observation or independent runtime corroboration confirms full-DR behavior.

## Top evidence files

- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` (31 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/frmItem.frm` (11 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/modMain.bas` (10 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/frmUniversalModifier.frm` (10 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/README.md` (7 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/frmPasteChar.frm` (6 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` (6 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` (5 hits)
- MMUDExplorer: `MMUDExplorer/MMUD-Explorer-master/docs/changelog.txt` (5 hits)
- NightmareRedux: `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` (5 hits)

## Representative evidence

- NightmareRedux `NightmareRedux/Nightmare-Redux-master/README.md` line 497: -UP: in-game AC and DR is calculated and shown on the item editor
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/docs/changelog.txt` line 523: -UP: in-game AC and DR is calculated and shown on the item editor
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/docs/notes.txt` line 9: [07:44] darkedgex: HEHEHE, you should update MMUD Explorer to let you pick which monster you're fighting in the BS calculator so it'll take off the DR. :D
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/docs/notes.txt` line 143: .Columns.Append "DR", adInteger			==> DamageResist
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/docs/notes.txt` line 202: .Columns.Append "DR", adInteger			==> DamageResist
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 2693: ts.Write ("Number" & vbTab & "Name" & vbTab & "Desc 1" & vbTab & "Desc 2" & vbTab & "Desc 3" & vbTab & "Desc 4" & vbTab & "Desc 5" & vbTab & "Desc 6" & vbTab & "Game Limit" & vbTab & "Weight" & vbTab & "Type" & vbTab & "Uses" & vbTab & "Cos
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 2736: ts.Write (Itemrec.DR & vbTab)
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 2867: tabItems.Fields("DR") = Itemrec.DR
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 4482: ts.Write ("Number" & vbTab & "Name" & vbTab & "Experience" & vbTab & "Index" & vbTab & "Weapon" & vbTab & "AC" & vbTab & "DR" & vbTab & "Follow" & vbTab & "MR" & vbTab & "HPs" & vbTab & "BS Defense" & vbTab & "Energy" & vbTab & "HP Regen" &
- NightmareRedux `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 4528: ts.Write (Monsterrec.DR & vbTab)

## Next action

Resolve the conflict before runtime implementation is marked classic-verified. Use Nightmare Redux as leading hypothesis only where source-priority policy applies.