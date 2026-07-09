# Weapon swing timing

**Mechanic ID:** `COMBAT.WEAPON_SWING`  
**Bucket:** Combat  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Weapon energy/speed handling and how multiple swings are derived.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 1233
- Representative evidence entries: 60
- Source/path coverage: 17

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/swing eq.txt` line 14 — `Swings=(((1+((eval(Swing_Calc.Agility.value)-50)*0.005))*(0.3+(eval(Swing_Calc.Level.value)*((eval(Swing_Calc.Combat.value)+2)*(1/150)))))*(5000/eval(Swing_Calc.Weapon_Speed.value)))*(1+(eval(Swing_Calc.Encumberance.value)*(40/1413)));`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 119 — `-UP: Added sample weapon speed/dmg/swings to character builder`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 31 — `-UP: Added sample weapon speed/dmg/swings to character builder`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.txt` line 113 — `-UP: Added sample weapon speed/dmg/swings to character builder`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 25835 — `oLI.ListSubItems.Add (14), "Dmg/Spd", Round(tWeaponDmg.nRoundTotal / tWeaponDmg.nSwings / tWeaponDmg.nAttackSpeed)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 31678 — `sOutput(0) = sOutput(0) & Space(nSpaceLen(0)) & "SPEED/DMG @ LVL/AGI: Weapon Swings (DMG)"`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMain.bas` line 4765 — `If nAttackTypeMUD <> a4_Surprise And nSpeed > 0 And tWeaponDmg.nRoundTotal > 0 And tWeaponDmg.nSwings > 0 Then`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMain.bas` line 4766 — `oLI.ListSubItems.Add (14), "Dmg/Spd", Round(tWeaponDmg.nRoundTotal / tWeaponDmg.nSwings / nSpeed, 4) * 1000`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/swing eq.txt` line 17 — `EU=Math.floor(EU*(eval(Swing_Calc.Speed.value)/100));`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmItem.frm` line 3648 — `Call frmSwingCalc.LoadWeapons`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmItem.frm` line 3653 — `frmSwingCalc.GotoWeapon (Val(lvDatabase.SelectedItem.Text))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm` line 464 — `Caption         =   "Energy per swing"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm` line 1764 — `Dim nWeaponSpeed As Currency, nEnergy As Currency, nQnDBonus As Currency`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm` line 1796 — `nEnergy = CalcEnergyUsedWithEncum(cmbCombat.ItemData(cmbCombat.ListIndex), Val(txtLevel.Text), nWeaponSpeed, _`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm` line 1836 — `lblEnergy.Caption = "Energy per swing: 0"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm` line 1837 — `lblEnergy.Caption = "Energy per swing: " & nEnergy`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm` line 1853 — `nSwings = Round((1000 / nEnergy), 4)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm` line 1855 — `lblEnergy.Caption = "Energy per swing: " & nEnergy`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 176 — `-UP: Added weapon swings and 1st round damage columns to weapon tab`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 178 — `-FIX: Weapon damage/swing calculation (but not the swing calculator) was flooring swings at 1 swing instead of 0`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 83 — `-UP: Added weapon swings and 1st round damage columns to weapon tab`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 85 — `-FIX: Weapon damage/swing calculation (but not the swing calculator) was flooring swings at 1 swing instead of 0`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.txt` line 165 — `-UP: Added weapon swings and 1st round damage columns to weapon tab`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/Nightmare Redux.vbp` line 62 — `Form=frmSwingCalc.frm`
- `MonsterSim` priority 5 — `MonsterSim/MajorMUD-PHP-Monster-Sim-master/capture_parse.php` line 43 — `$i++; $attacks[$i][1] = 'CLAW'; $attacks[$i][2] = 'SOURCE claws you for DMG damage!'; $attacks[$i][3] = 'SOURCE swings at you with its huge claws!';`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
