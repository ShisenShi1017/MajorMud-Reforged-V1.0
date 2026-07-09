# Physical damage reduction

**Mechanic ID:** `COMBAT.DR_PHYSICAL`  
**Bucket:** Combat  
**Status:** open_conflict  
**Runtime:** not_changed_by_phase2_population

## Description
How physical DR reduces final melee damage; current conflict prefers Nightmare Redux pending original runtime corroboration.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 953
- Representative evidence entries: 42
- Source/path coverage: 12

## Notes
- Conflict carried forward: Monster Sim historical interpretation differs from Nightmare Redux current interpretation. Project policy prefers Nightmare Redux as leading hypothesis until original runtime evidence proves otherwise.

## Representative Evidence

- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 23166 — `MsgBox "Enter the average of the number of swings from each melee character. This is utilized to help determine DR reduction. " _`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmPasteChar.frm` line 3004 — `MsgBox "Enter the average of the number of swings from each melee character. This is utilized to help determine DR reduction. " _`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 497 — `-UP: in-game AC and DR is calculated and shown on the item editor`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/changelog.txt` line 523 — `-UP: in-game AC and DR is calculated and shown on the item editor`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/notes.txt` line 9 — `[07:44] darkedgex: HEHEHE, you should update MMUD Explorer to let you pick which monster you're fighting in the BS calculator so it'll take off the DR. :D`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/notes.txt` line 143 — `.Columns.Append "DR", adInteger			==> DamageResist`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/notes.txt` line 202 — `.Columns.Append "DR", adInteger			==> DamageResist`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 2693 — `ts.Write ("Number" & vbTab & "Name" & vbTab & "Desc 1" & vbTab & "Desc 2" & vbTab & "Desc 3" & vbTab & "Desc 4" & vbTab & "Desc 5" & vbTab & "Desc 6" & vbTab & "Game Limit" & vbTab & "Weight" & vbTab & "Type" & vbTab & "Uses" & vbTab & "Cost" & vbTab & "Cost Type" & vbTab & "Min Hit" & vbTab & "Max Hit" & vbTab & "Weapon" & vbTab & "Armour" & vbTab & "WornOn" & vbTab & "Accuracy" & vbTab & "DR" & `
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 2736 — `ts.Write (Itemrec.DR & vbTab)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 2867 — `tabItems.Fields("DR") = Itemrec.DR`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 4482 — `ts.Write ("Number" & vbTab & "Name" & vbTab & "Experience" & vbTab & "Index" & vbTab & "Weapon" & vbTab & "AC" & vbTab & "DR" & vbTab & "Follow" & vbTab & "MR" & vbTab & "HPs" & vbTab & "BS Defense" & vbTab & "Energy" & vbTab & "HP Regen" & vbTab & "Game Limit" & vbTab & "Active" & vbTab & "Type" & vbTab & "Alignment" & vbTab & "Gender" & vbTab & "Group" & vbTab & "Regen Time" & vbTab)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 4528 — `ts.Write (Monsterrec.DR & vbTab)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 4691 — `tabMonsters.Fields("DR") = Monsterrec.DR`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseImport.frm` line 1813 — `Itemrec.DR = tabItems.Fields("DR")`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 50 — `-FIX: Armor comparison calculation for AC/DR +/- (it was rounding decimals)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 58 — `-NEW: By popular demand, more monster filter options (accuracy, dodge, coin types, ac/dr, etc)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 169 — `-FIX: Decimal values for manual AC/DR adjustments`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 229 — `-UP: Added columns for AC/DR, Dodge, and MR`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 242 — `-NEW: Full damage calculation of weapons with consideration of crits, max damage, and accuracy versues mob AC/DR and dodge`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 454 — `-FIX: reported DR amount in spell formula when value is scaled`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 713 — `-FIX: AC and DR calculation issues on inventory`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 77 — `-FIX: Decimal values for manual AC/DR adjustments`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 135 — `-UP: Added columns for AC/DR, Dodge, and MR`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 148 — `-NEW: Full damage calculation of weapons with consideration of crits, max damage, and accuracy versues mob AC/DR and dodge`
- `MonsterSim` priority 5 — `MonsterSim/MajorMUD-PHP-Monster-Sim-master/monster.php` line 164 — `<label for="character_dr">DR:</label>`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
