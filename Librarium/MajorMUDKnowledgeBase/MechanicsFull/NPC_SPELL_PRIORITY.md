# NPC spell choice and priority

**Mechanic ID:** `NPC.SPELL_PRIORITY`  
**Bucket:** NPC AI  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
How NPCs select spells, including energy and target conditions.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 5678
- Representative evidence entries: 60
- Source/path coverage: 21

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 835 — `' BETWEEN ROUND SPELL CAST`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 4579 — `ts.Write (Monsterrec.SpellCastPer(x) & vbTab)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 4580 — `ts.Write (Monsterrec.SpellCastLvl(x) & vbTab)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 4757 — `tabMonsters.Fields("Spell Cast % " & x) = Monsterrec.SpellCastPer(x)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 4758 — `tabMonsters.Fields("Spell Cast LVL " & x) = Monsterrec.SpellCastLvl(x)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseImport.frm` line 2892 — `Monsterrec.SpellCastPer(x) = tabMonsters.Fields("Spell Cast % " & x)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseImport.frm` line 2893 — `Monsterrec.SpellCastLvl(x) = tabMonsters.Fields("Spell Cast LVL " & x)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 1865 — `clsMonAtkSim.nAtkDuration(x) = GetSpellDuration(Monsterrec.AttackAccuSpell(x), Monsterrec.AttackMaxHCastLvl(x))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 1876 — `clsMonAtkSim.nAtkMin(x) = GetSpellMinDamage(Monsterrec.AttackAccuSpell(x), Monsterrec.AttackMaxHCastLvl(x))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 1877 — `clsMonAtkSim.nAtkMax(x) = GetSpellMaxDamage(Monsterrec.AttackAccuSpell(x), Monsterrec.AttackMaxHCastLvl(x))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 1888 — `clsMonAtkSim.nAtkMin(x) = GetSpellMinDamage(Monsterrec.AttackAccuSpell(x), Monsterrec.AttackMaxHCastLvl(x))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 1889 — `clsMonAtkSim.nAtkMax(x) = GetSpellMaxDamage(Monsterrec.AttackAccuSpell(x), Monsterrec.AttackMaxHCastLvl(x))`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 93 — `-FIX: Area effect spells cast by monsters utilizing abil-1 being marked as invalid for [GreaterMUD] (they ARE still invalid for stock)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 940 — `' BETWEEN ROUND SPELL CAST`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.txt` line 90 — `-FIX: Area effect spells cast by monsters utilizing abil-1 being marked as invalid for [GreaterMUD] (they ARE still invalid for stock)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 36045 — `x = MsgBox("Clear saved weapon, armour, spell, and monsters lists?  The item manager is cleared regardless.", vbYesNoCancel + vbQuestion + vbDefaultButton2)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudDatabase.bas` line 4598 — `GetSpellMinDamage = GetSpellMinDamage + GetSpellMinDamage(nEndCast, nCastLevel, nEnergyRem, bForMonster)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudDatabase.bas` line 4680 — `GetSpellMaxDamage = GetSpellMaxDamage + GetSpellMaxDamage(nEndCast, nCastLevel, nEnergyRem, bForMonster)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudDatabase.bas` line 4692 — `Public Function GetSpellDuration(ByVal nSpellNumber As Long, Optional ByVal nCastLevel As Integer, Optional bForMonster As Boolean) As Long`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMain.bas` line 2860 — `oLI.ListSubItems.Add (1), "Detail", "This is an invalid spell and will not be cast (area attack spells from regular monster casts must use ability 17, Damage-MR)."`
- `MonsterSim` priority 5 — `MonsterSim/MajorMUD-PHP-Monster-Sim-master/monster.php` line 472 — `p(damage('Average Damage/'.($attack['type'] == 'spell' ? (($attack['attempted'] > $attack['hits']) ? 'Cast (including fails)':'Cast') : 'Swing').': '.round($attack['total_damage']/$attack['attempted'],2)));`
- `MonsterSim` priority 5 — `MonsterSim/MajorMUD-PHP-Monster-Sim-master/monster.php` line 473 — `p('Average '.($attack['type'] == 'spell' ? 'Casts' : 'Swings').'/Round: '.round($attack['attempted']/$number_of_rounds,2));`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/CLAUDE.md` line 65 — ``Recordset` objects opened once at load: `tabItems`, `tabMonsters`, `tabSpells`, `tabRooms`,`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 3 — `MMUD Explorer is a database viewer for the game MajorMUD(r) created by syntax53. It has a unique comparing feature which allows you to easily compare weapons, armour, and spells. It also has a graphical room explorer in which you can 'walk' around the realm. Other features include an inventory calculator, exp calculator, explorers for monsters/shops/weapons/armour/spells/items/races/classes, savin`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 67 — `-UP: Can now right click on scroll references on spell tabs to add the scroll to the item manager (build a shopping list)`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
