# Physical damage roll

**Mechanic ID:** `COMBAT.DAMAGE_ROLL`  
**Bucket:** Combat  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
How min/max damage, bonuses, and reductions produce final physical damage.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 2132
- Representative evidence entries: 60
- Source/path coverage: 21

## Representative Evidence

- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas` line 1188 — `Dim nPreRollMinModifier As Double, nPreRollMaxModifier As Double, nDamageMultiplierMin As Double, nDamageMultiplierMax As Double`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 2693 — `ts.Write ("Number" & vbTab & "Name" & vbTab & "Desc 1" & vbTab & "Desc 2" & vbTab & "Desc 3" & vbTab & "Desc 4" & vbTab & "Desc 5" & vbTab & "Desc 6" & vbTab & "Game Limit" & vbTab & "Weight" & vbTab & "Type" & vbTab & "Uses" & vbTab & "Cost" & vbTab & "Cost Type" & vbTab & "Min Hit" & vbTab & "Max Hit" & vbTab & "Weapon" & vbTab & "Armour" & vbTab & "WornOn" & vbTab & "Accuracy" & vbTab & "DR" & `
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 3774 — `ts.Write ("Number" & vbTab & "Name" & vbTab & "Min HP" & vbTab & "Max HP" & vbTab & "EXP %" & vbTab & "Magic LVL" & vbTab & "Combat Level" & vbTab & "Title Text" & vbTab & "Magic Type" & vbTab & "Weapon" & vbTab & "Armour" & vbTab)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 1855 — `& "This is an error and MMUD will not cast this.  Area attack spells must use ability 17 (or possibly 8-drain?).  The min/max damage and energy cost has been zero'd out for the sim to reflect the game.", vbExclamation`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 1876 — `clsMonAtkSim.nAtkMin(x) = GetSpellMinDamage(Monsterrec.AttackAccuSpell(x), Monsterrec.AttackMaxHCastLvl(x))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 1888 — `clsMonAtkSim.nAtkMin(x) = GetSpellMinDamage(Monsterrec.AttackAccuSpell(x), Monsterrec.AttackMaxHCastLvl(x))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 1900 — `clsMonAtkSim.nAtkMin(x) = GetSpellMinDamage(Monsterrec.AttackAccuSpell(x), Monsterrec.AttackMaxHCastLvl(x))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonsterAttackSim.frm` line 4860 — `& "This is an error and MMUD will not cast this.  Area attack spells must use ability 17 (or possibly 8-drain?).  The min/max damage and energy cost has been zero'd out for the sim to reflect the game.", vbExclamation`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonsterAttackSim.frm` line 4882 — `txtAtkMin(x).Text = GetSpellMinDamage(Monsterrec.AttackAccuSpell(x), Monsterrec.AttackMaxHCastLvl(x))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonsterAttackSim.frm` line 4894 — `txtAtkMin(x).Text = GetSpellMinDamage(Monsterrec.AttackAccuSpell(x), Monsterrec.AttackMaxHCastLvl(x))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonsterAttackSim.frm` line 4906 — `txtAtkMin(x).Text = GetSpellMinDamage(Monsterrec.AttackAccuSpell(x), Monsterrec.AttackMaxHCastLvl(x))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonsterAttackSim.frm` line 5106 — `MsgBox "Items that are *ON* the monster will give them bonuses to ACCY and +min/max damage... " _`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonsterAttackSim.frm` line 5608 — `sText = sText & "Physical Min/Max Dmg: " & IIf(nItemDamageBonus > 0, "+", "") & nItemDamageBonus`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 242 — `-NEW: Full damage calculation of weapons with consideration of crits, max damage, and accuracy versues mob AC/DR and dodge`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 413 — `-FIX: min/max spell damage was sometimes incorrect if the cast level was outside the spell's min/max level caps`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 435 — `-UP: separated damage into min/max so it is sortable`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 438 — `-UP: separated damage into min/max so it is sortable`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 148 — `-NEW: Full damage calculation of weapons with consideration of crits, max damage, and accuracy versues mob AC/DR and dodge`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 320 — `-FIX: min/max spell damage was sometimes incorrect if the cast level was outside the spell's min/max level caps`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 342 — `-UP: separated damage into min/max so it is sortable`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 345 — `-UP: separated damage into min/max so it is sortable`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.txt` line 230 — `-NEW: Full damage calculation of weapons with consideration of crits, max damage, and accuracy versues mob AC/DR and dodge`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmBSCalc.frm` line 1006 — `Dim x As Integer, bClassStealth As Boolean, nPlusMaxDamage As Integer, nMinStrBonus As Integer`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 31562 — `Dim nMinRoll As Long, nMaxRoll As Long, nLevelSteps(4) As Integer, nHealth As Integer, sTmp As String`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveUtil_Test.cs` line 19 — `return base.Read(buffer, offset, Math.Min(count, MaxBytesToRead));`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
