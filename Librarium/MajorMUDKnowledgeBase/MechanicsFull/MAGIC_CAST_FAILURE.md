# Cast failure handling

**Mechanic ID:** `MAGIC.CAST_FAILURE`  
**Bucket:** Magic  
**Status:** high_confidence_verified  
**Runtime:** not_changed_by_phase2_population

## Description
Failure/resist outcomes and resource refund or loss behavior.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 894
- Representative evidence entries: 60
- Source/path coverage: 17

## Representative Evidence

- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMain.bas` line 4426 — `'===== Handle cast failures (refund half cost on average) =====`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMain.bas` line 4431 — `' One failed cast event: refund half the cost`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 163 — `-UP: spell calculations will now take energy into account`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 313 — `Or m_nAtkSuccess(nAttack) < 100 Then 'resistable or failable`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 419 — `Dim nRemainingEnergy As Long, nEnergyUsed As Integer, bShowHitSpell As Boolean`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 807 — `& IIf(bResisted, "RESIST", "FAIL") & ")", _`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/changelog.txt` line 176 — `-UP: spell calculations will now take energy into account`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 3398 — `ts.Write (Spellrec.Energy & vbTab)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 3515 — `tabSpells.Fields("Energy") = Spellrec.Energy`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseImport.frm` line 2163 — `Spellrec.Energy = tabSpells.Fields("Energy")`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 1855 — `& "This is an error and MMUD will not cast this.  Area attack spells must use ability 17 (or possibly 8-drain?).  The min/max damage and energy cost has been zero'd out for the sim to reflect the game.", vbExclamation`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 2165 — `tabSpells.Fields("EnergyCost") = 0`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 5055 — `tabSpells.Fields("EnergyCost") = Spellrec.Energy`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonsterAttackSim.frm` line 4860 — `& "This is an error and MMUD will not cast this.  Area attack spells must use ability 17 (or possibly 8-drain?).  The min/max damage and energy cost has been zero'd out for the sim to reflect the game.", vbExclamation`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveFileProcessor_Tests.cs` line 463 — `public void InsertionConstraintFailure()`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveFileProcessor_Tests.cs` line 473 — `Key1 = 3444, // constraint failure here`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveFileProcessor_Tests.cs` line 543 — `// constraint failure here`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/DOS/Interrupts/Int7Bh_Tests.cs` line 640 — `public void UpdateKeyConstraintFailure()`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Integration/BtrievePrint_Tests.cs` line 656 — `public void AddItems_KeyConstraint_Failure()`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 375 — `Or m_nAtkSuccess(nAttack) < 100 Then 'resistable or failable`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 481 — `Dim nRemainingEnergy As Long, nEnergyUsed As Integer, bShowHitSpell As Boolean`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 912 — `& IIf(bResisted, "RESIST", "FAIL") & ")", _`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudDatabase.bas` line 1934 — `If tabSpells.Fields("EnergyCost") > 0 And tabSpells.Fields("EnergyCost") <= 500 Then`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudDatabase.bas` line 1935 — `GetSpellManaCost = GetSpellManaCost * Fix(1000 / tabSpells.Fields("EnergyCost"))`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudDatabase.bas` line 3453 — `nCostCopper = nCost * 10000`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
