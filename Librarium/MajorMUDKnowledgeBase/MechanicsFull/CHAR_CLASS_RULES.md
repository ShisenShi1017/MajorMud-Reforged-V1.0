# Class modifiers and restrictions

**Mechanic ID:** `CHAR.CLASS_RULES`  
**Bucket:** Character Progression  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Class-specific formulas, restrictions, and ability access.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 1325
- Representative evidence entries: 59
- Source/path coverage: 19

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmClass.frm` line 965 — `oLI.ListSubItems(5).Text = GetMagery(Classrec.MagicType, Classrec.MagicLvL)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmClass.frm` line 1046 — `oLI.ListSubItems.add (5), "Magic", GetMagery(Classrec.MagicType, Classrec.MagicLvL)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 5140 — `tabClasses.Fields("MageryType") = Classrec.MagicType`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 5141 — `tabClasses.Fields("MageryLVL") = Classrec.MagicLvL`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/CLAUDE.md` line 66 — ``tabShops`, `tabRaces`, `tabClasses`, `tabLairs`, etc. Combat/damage result arrays`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmBSCalc.frm` line 1006 — `Dim x As Integer, bClassStealth As Boolean, nPlusMaxDamage As Integer, nMinStrBonus As Integer`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmBSCalc.frm` line 1123 — `nMinDmg = CalcBSDamage(val(txtLevel.Text), nStealth, nMinDmg, nPlusBSmindmg, bClassStealth)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmBSCalc.frm` line 1124 — `nMaxDmg = CalcBSDamage(val(txtLevel.Text), nStealth, nMaxDmg, nPlusBSmaxdmg, bClassStealth)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 21677 — `Dim nLevel As Long, nClass As Long ', nMagery As Integer, nMageryLVL As Integer`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 27855 — `Case 1: 'warrior/paladin/cleric/missy/ninja/thief/bard/gypsy/warlock/ranger/mystic: 1 ac + 1 crit`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 28909 — `LastFindBest(x) = tabItems.Fields("ArmourClass") + tabItems.Fields("DamageResist")`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 28965 — `nEnc_Ratio = Get_Enc_Ratio(tabItems.Fields("Encum"), tabItems.Fields("ArmourClass"), tabItems.Fields("DamageResist"))`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 28968 — `If (tabItems.Fields("ArmourClass") + tabItems.Fields("DamageResist") > tPosWinner(x).Value) _`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 28969 — `Or (tabItems.Fields("ArmourClass") + tabItems.Fields("DamageResist") = tPosWinner(x).Value _`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 28974 — `tPosWinner(x).Value = tabItems.Fields("ArmourClass") + tabItems.Fields("DamageResist")`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 35686 — `'Case 1: 'warrior/paladin/cleric/missy/ninja/thief/bard/gypsy/warlock/ranger/mystic: 1 ac + 1 crit`
- `MonsterSim` priority 5 — `MonsterSim/MajorMUD-PHP-Monster-Sim-master/monster.php` line 374 — `<td><span class="magenta bold"><?php echo (round($attack['attempted']/$total_attacks, 3)*100); ?>%</span></td><!-- True Cast -->`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 36 — `9046|0|0|flag checks and class rewards`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 317 — `9537|0|9483|exp and flags - executed by dark mage's death spell`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 392 — `'this is kind of confusing because after the flag check there are actually 2 text blocks used. each one uses the 511 block, then the others are class specific.`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 498 — `9537|0|9483|death spell of dark mage -- flag check, give 175 mill exp bonus`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 507 — `9474|0|0|class/flag checks, then rewards`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 508 — `9474|1|0|class/flag checks, then rewards`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 709 — `9489|0|0|class/flag checks and rewards`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 761 — `4327|0|0|tele for solo trainer, class filtered`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
