# NPC spell energy and double-cast behavior

**Mechanic ID:** `MAGIC.NPC_SPELL_ENERGY`  
**Bucket:** Magic  
**Status:** high_confidence_verified  
**Runtime:** not_changed_by_phase2_population

## Description
NPC spell cost/refund behavior including half-cost on failed/resisted spell and possible second cast.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 2363
- Representative evidence entries: 60
- Source/path coverage: 18

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 1855 — `& "This is an error and MMUD will not cast this.  Area attack spells must use ability 17 (or possibly 8-drain?).  The min/max damage and energy cost has been zero'd out for the sim to reflect the game.", vbExclamation`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonsterAttackSim.frm` line 4860 — `& "This is an error and MMUD will not cast this.  Area attack spells must use ability 17 (or possibly 8-drain?).  The min/max damage and energy cost has been zero'd out for the sim to reflect the game.", vbExclamation`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudDatabase.bas` line 4528 — `Public Function GetSpellMinDamage(ByVal nSpellNumber As Long, Optional ByVal nCastLevel As Integer, Optional nEnergyRem As Integer, _`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudDatabase.bas` line 4598 — `GetSpellMinDamage = GetSpellMinDamage + GetSpellMinDamage(nEndCast, nCastLevel, nEnergyRem, bForMonster)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudDatabase.bas` line 4610 — `Public Function GetSpellMaxDamage(ByVal nSpellNumber As Long, Optional ByVal nCastLevel As Integer, Optional nEnergyRem As Integer, _`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudDatabase.bas` line 4680 — `GetSpellMaxDamage = GetSpellMaxDamage + GetSpellMaxDamage(nEndCast, nCastLevel, nEnergyRem, bForMonster)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudDatabase.bas` line 5498 — `& "This is an error and MMUD will not cast this.  Area attack spells must use ability 17 (or possibly 8-drain?).  The min/max damage and energy cost has been zero'd out for the sim to reflect the game.", vbExclamation`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas` line 987 — `nCasts = Fix(1000 / tabSpells.Fields("EnergyCost"))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 163 — `-UP: spell calculations will now take energy into account`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 419 — `Dim nRemainingEnergy As Long, nEnergyUsed As Integer, bShowHitSpell As Boolean`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/changelog.txt` line 176 — `-UP: spell calculations will now take energy into account`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 3398 — `ts.Write (Spellrec.Energy & vbTab)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 3515 — `tabSpells.Fields("Energy") = Spellrec.Energy`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseImport.frm` line 2163 — `Spellrec.Energy = tabSpells.Fields("Energy")`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 2165 — `tabSpells.Fields("EnergyCost") = 0`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 5055 — `tabSpells.Fields("EnergyCost") = Spellrec.Energy`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmSpell.frm` line 2037 — `If Spellrec.Energy > nVal Then bAdd = False`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmSpell.frm` line 2038 — `If chkFilterExcludeZero.Value = 1 And Spellrec.Energy = 0 Then bAdd = False`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 481 — `Dim nRemainingEnergy As Long, nEnergyUsed As Integer, bShowHitSpell As Boolean`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudDatabase.bas` line 1934 — `If tabSpells.Fields("EnergyCost") > 0 And tabSpells.Fields("EnergyCost") <= 500 Then`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudDatabase.bas` line 1935 — `GetSpellManaCost = GetSpellManaCost * Fix(1000 / tabSpells.Fields("EnergyCost"))`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudDatabase.bas` line 3453 — `nCostCopper = nCost * 10000`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudDatabase.bas` line 3455 — `nCostCopper = nCost * 1000000`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudDatabase.bas` line 3517 — `nCopperBuy = nBaseCost * 10000`
- `MonsterSim` priority 5 — `MonsterSim/MajorMUD-PHP-Monster-Sim-master/monster.php` line 129 — `<th>#</th><th>Name</th><th>Type</th><th>Resistable</th><th>Energy</th><th>Min</th><th>Max</th><th>Attack<br>Chance</th><th>Accy. or<br>Cast%</th>`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
