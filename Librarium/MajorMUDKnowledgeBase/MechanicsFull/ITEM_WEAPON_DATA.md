# Weapon data interpretation

**Mechanic ID:** `ITEM.WEAPON_DATA`  
**Bucket:** Items and Equipment  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Weapon damage/speed/type/special fields.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 2104
- Representative evidence entries: 60
- Source/path coverage: 24

## Representative Evidence

- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMain.bas` line 4765 — `If nAttackTypeMUD <> a4_Surprise And nSpeed > 0 And tWeaponDmg.nRoundTotal > 0 And tWeaponDmg.nSwings > 0 Then`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/swing eq.txt` line 14 — `Swings=(((1+((eval(Swing_Calc.Agility.value)-50)*0.005))*(0.3+(eval(Swing_Calc.Level.value)*((eval(Swing_Calc.Combat.value)+2)*(1/150)))))*(5000/eval(Swing_Calc.Weapon_Speed.value)))*(1+(eval(Swing_Calc.Encumberance.value)*(40/1413)));`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm` line 1764 — `Dim nWeaponSpeed As Currency, nEnergy As Currency, nQnDBonus As Currency`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm` line 1785 — `nWeaponSpeed = Itemrec.Speed 'tabItems.Fields("Speed")`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm` line 1796 — `nEnergy = CalcEnergyUsedWithEncum(cmbCombat.ItemData(cmbCombat.ListIndex), Val(txtLevel.Text), nWeaponSpeed, _`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 119 — `-UP: Added sample weapon speed/dmg/swings to character builder`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 176 — `-UP: Added weapon swings and 1st round damage columns to weapon tab`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 178 — `-FIX: Weapon damage/swing calculation (but not the swing calculator) was flooring swings at 1 swing instead of 0`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 31 — `-UP: Added sample weapon speed/dmg/swings to character builder`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 83 — `-UP: Added weapon swings and 1st round damage columns to weapon tab`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 85 — `-FIX: Weapon damage/swing calculation (but not the swing calculator) was flooring swings at 1 swing instead of 0`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.txt` line 113 — `-UP: Added sample weapon speed/dmg/swings to character builder`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.txt` line 165 — `-UP: Added weapon swings and 1st round damage columns to weapon tab`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.txt` line 167 — `-FIX: Weapon damage/swing calculation (but not the swing calculator) was flooring swings at 1 swing instead of 0`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 25835 — `oLI.ListSubItems.Add (14), "Dmg/Spd", Round(tWeaponDmg.nRoundTotal / tWeaponDmg.nSwings / tWeaponDmg.nAttackSpeed)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 31678 — `sOutput(0) = sOutput(0) & Space(nSpaceLen(0)) & "SPEED/DMG @ LVL/AGI: Weapon Swings (DMG)"`
- `MonsterSim` priority 5 — `MonsterSim/MajorMUD-PHP-Monster-Sim-master/monster.php` line 472 — `p(damage('Average Damage/'.($attack['type'] == 'spell' ? (($attack['attempted'] > $attack['hits']) ? 'Cast (including fails)':'Cast') : 'Swing').': '.round($attack['total_damage']/$attack['attempted'],2)));`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/swing eq.txt` line 17 — `EU=Math.floor(EU*(eval(Swing_Calc.Speed.value)/100));`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmClass.frm` line 963 — `oLI.ListSubItems(3).Text = GetClassWeaponType(Classrec.Weapon)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmClass.frm` line 1044 — `oLI.ListSubItems.add (3), "Weapon", GetClassWeaponType(Classrec.Weapon)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 2693 — `ts.Write ("Number" & vbTab & "Name" & vbTab & "Desc 1" & vbTab & "Desc 2" & vbTab & "Desc 3" & vbTab & "Desc 4" & vbTab & "Desc 5" & vbTab & "Desc 6" & vbTab & "Game Limit" & vbTab & "Weight" & vbTab & "Type" & vbTab & "Uses" & vbTab & "Cost" & vbTab & "Cost Type" & vbTab & "Min Hit" & vbTab & "Max Hit" & vbTab & "Weapon" & vbTab & "Armour" & vbTab & "WornOn" & vbTab & "Accuracy" & vbTab & "DR" & `
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 3774 — `ts.Write ("Number" & vbTab & "Name" & vbTab & "Min HP" & vbTab & "Max HP" & vbTab & "EXP %" & vbTab & "Magic LVL" & vbTab & "Combat Level" & vbTab & "Title Text" & vbTab & "Magic Type" & vbTab & "Weapon" & vbTab & "Armour" & vbTab)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 4482 — `ts.Write ("Number" & vbTab & "Name" & vbTab & "Experience" & vbTab & "Index" & vbTab & "Weapon" & vbTab & "AC" & vbTab & "DR" & vbTab & "Follow" & vbTab & "MR" & vbTab & "HPs" & vbTab & "BS Defense" & vbTab & "Energy" & vbTab & "HP Regen" & vbTab & "Game Limit" & vbTab & "Active" & vbTab & "Type" & vbTab & "Alignment" & vbTab & "Gender" & vbTab & "Group" & vbTab & "Regen Time" & vbTab)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmItem.frm` line 3648 — `Call frmSwingCalc.LoadWeapons`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmItem.frm` line 3653 — `frmSwingCalc.GotoWeapon (Val(lvDatabase.SelectedItem.Text))`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
