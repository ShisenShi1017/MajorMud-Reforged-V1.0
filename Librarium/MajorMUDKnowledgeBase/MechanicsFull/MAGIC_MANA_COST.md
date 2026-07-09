# Spell mana cost

**Mechanic ID:** `MAGIC.MANA_COST`  
**Bucket:** Magic  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
How player spell mana cost is interpreted from spell data and modifiers.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 4619
- Representative evidence entries: 60
- Source/path coverage: 19

## Representative Evidence

- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modExpPerHour.bas` line 2715 — `' When there is no direct spell cost, keep the old fractional overhead path`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modExpPerHour.bas` line 4210 — `' Spell cost is paid per combat round; overhead/upkeep is only paid`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modExpPerHour.bas` line 4311 — `' When there is no direct spell cost, keep overhead fractional so passive`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmPopUpOptions.frm` line 1268 — `'Private nLocalCurrentAttackSpellCost As Long`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmPopUpOptions.frm` line 1270 — `'Private nLocalCurrentHealSpellCost As Long`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmPopUpOptions.frm` line 2079 — `Dim nLocalAttackSpellLVL As Long, nLocalAttackSpellCost As Double, nLocalAttackSpellNum As Long`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmPopUpOptions.frm` line 2096 — `nLocalAttackSpellCost = GetSpellManaCost(nLocalAttackSpellNum)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmPopUpOptions.frm` line 2205 — `tTEMPchar.nSpellOverhead = nLocalAttackSpellCost + (val(frmMain.lblCharBless.Caption) / 6)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmPopUpOptions.frm` line 2222 — `nLocalHealCost = Round(tHealSpell.nManaCost / nLocalHealRounds, 2)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmPopUpOptions.frm` line 2226 — `nLocalHealRoundsOOM = CalcRoundsToOOM(nLocalHealCost + nLocalAttackSpellCost + (val(frmMain.lblCharBless.Caption) / 6), _`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmPopUpOptions.frm` line 2288 — `lblHealMANAPerRound.Caption = nLocalHealCost & " mana/rnd"`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmPopUpOptions.frm` line 2292 — `If nLocalAttackSpellCost > 0 Then lblHealManaOOM.Caption = lblHealManaOOM.Caption & "+"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 910 — `'little complex here - block 1448 casts spell 614, which gives +0 charm for`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/notes.txt` line 115 — `.Columns.Append "Mana", adInteger			==> ManaCost`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseImport.frm` line 2160 — `Spellrec.CastMsgA = tabSpells.Fields("Cast MSG A")`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseImport.frm` line 2161 — `Spellrec.CastMsgB = tabSpells.Fields("Cast MSG B")`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseImport.frm` line 2164 — `Spellrec.Mana = tabSpells.Fields("Mana")`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseImport.frm` line 2884 — `Monsterrec.AttackMinHCastPer(x) = tabMonsters.Fields("Attack Min Hit/Cast % " & x)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseImport.frm` line 2885 — `Monsterrec.AttackMaxHCastLvl(x) = tabMonsters.Fields("Attack Max Hit/Cast LVL " & x)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseImport.frm` line 2892 — `Monsterrec.SpellCastPer(x) = tabMonsters.Fields("Spell Cast % " & x)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseImport.frm` line 2893 — `Monsterrec.SpellCastLvl(x) = tabMonsters.Fields("Spell Cast LVL " & x)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 2166 — `tabSpells.Fields("ManaCost") = 0`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 2401 — `sTemp = tabSpells.Fields("Casted By")`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 2417 — `tabSpells.Fields("Casted By") = sTemp`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveFile_Tests.cs` line 26 — `CopyFilesToTempPath(ResourceManager.GetTestResourceManager());`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
