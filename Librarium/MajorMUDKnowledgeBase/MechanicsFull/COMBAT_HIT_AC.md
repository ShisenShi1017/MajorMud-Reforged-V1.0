# Physical hit chance vs armor class

**Mechanic ID:** `COMBAT.HIT_AC`  
**Bucket:** Combat  
**Status:** high_confidence_verified  
**Runtime:** not_changed_by_phase2_population

## Description
How attacker accuracy/level/attack values interact with defender AC before damage is rolled.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 8815
- Representative evidence entries: 60
- Source/path coverage: 30

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 579 — `' CHANCE TO HIT / CAST`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 593 — `'PHYSICAL ATTACKS = 9% MIN HIT CHANCE (stock), 99% MAX`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 648 — `' CHANCE TO HIT / CAST`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 662 — `'PHYSICAL ATTACKS = 9% MIN HIT CHANCE, 99% MAX (stock)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 423 — `Dim nRoll_AttackChance As Integer, nRoll_HitChance As Integer, nRoll_DodgeChance As Integer, nAdjustedDodgeValue As Integer`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 583 — `'=((AC*AC)/100)/((ACCY*ACCY)/140)=fail %`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/notes.txt` line 170 — `.Columns.Append CStr("Attack Miss Msg " & x), adInteger		==> <take out>`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 4492 — `ts.Write ("Attack Miss Msg " & x & vbTab)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 4575 — `ts.Write (Monsterrec.AttackMissMsg(x) & vbTab)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 4753 — `tabMonsters.Fields("Attack Miss Msg " & x) = Monsterrec.AttackMissMsg(x)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseImport.frm` line 2888 — `Monsterrec.AttackMissMsg(x) = tabMonsters.Fields("Attack Miss Msg " & x)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonster.frm` line 2646 — `Tab(0).Control(2)=   "lblAttackMissMsg(50)"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonster.frm` line 2666 — `Tab(0).Control(12)=   "txtAttackMissMsgDisplay(0)"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonster.frm` line 2676 — `Tab(0).Control(17)=   "txtAttackMissMsg(0)"`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 187 — `-NEW: Critical Hit Chance formula discovered and now calculated automatically`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 419 — `-UP: added AC value to show miss rate vs accuracy values`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 485 — `Dim nRoll_AttackChance As Integer, nRoll_HitChance As Integer, nRoll_DodgeChance As Integer, nAdjustedDodgeValue As Long`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 652 — `'=((AC*AC)/100)/((ACCY*ACCY)/140)=fail %`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 93 — `-NEW: Critical Hit Chance formula discovered and now calculated automatically`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 326 — `-UP: added AC value to show miss rate vs accuracy values`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.txt` line 175 — `-NEW: Critical Hit Chance formula discovered and now calculated automatically`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMonsterAttackSim.frm` line 1557 — `Caption         =   "AC"`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modExpPerHour.bas` line 1578 — `'nSurpriseChance = Chance to hit nSurpriseDMG`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas` line 1721 — `nPercent2 = 1 - nPercent 'chance for regular hit chance`
- `MonsterSim` priority 5 — `MonsterSim/MajorMUD-PHP-Monster-Sim-master/capture_parse.php` line 41 — `$i=0; $attacks = array(); //1=name, 2=hit msg, 3=miss/fail msg, 4=resist`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
