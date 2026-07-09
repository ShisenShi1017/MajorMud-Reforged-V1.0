# Magic resistance chance

**Mechanic ID:** `MAGIC.MR_RESIST`  
**Bucket:** Magic  
**Status:** high_confidence_verified  
**Runtime:** not_changed_by_phase2_population

## Description
Chance that magic resistance prevents or resists a spell effect.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 2731
- Representative evidence entries: 60
- Source/path coverage: 25

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmUser.frm` line 4210 — `Caption         =   "Magic Resistance:"`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 189 — `-NEW: Magic Resistance formula discovered and now calculated automatically`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 95 — `-NEW: Magic Resistance formula discovered and now calculated automatically`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.txt` line 177 — `-NEW: Magic Resistance formula discovered and now calculated automatically`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 10297 — `Caption         =   "Magic Resistance"`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 17572 — `ToolTipText     =   "Average Magic Resistance of characters in party"`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 27978 — `'| Magic Resistance`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 13951 — `Caption         =   "Magic Resist:"`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 28027 — `'magic resist`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonsterAttackSim.frm` line 5118 — `MsgBox "Note: MR < 50 gives negative resistance when MR is taken into account.", vbInformation`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 418 — `-UP: added MR value corresponding resistance values`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 325 — `-UP: added MR value corresponding resistance values`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMonsterAttackSim.frm` line 2081 — `MsgBox "Note: MR < 50 gives negative resistance when MR is taken into account.", vbInformation`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 164 — `-UP: added boxes to show spell damage vs. X MR for damage spells`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 314 — `-NEW: "Charm Resistance" field for monsters added (thx Tsunami)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 31 — `Private m_nAtkMRdmgResist(0 To 4) As Integer`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 84 — `Private m_nBetweenRoundResistDmgMR(0 To 4) As Integer`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 177 — `Public Property Get nBetweenRoundResistDmgMR(ByVal Index As Integer) As Integer`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 178 — `nBetweenRoundResistDmgMR = m_nBetweenRoundResistDmgMR(Index)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 181 — `Public Property Let nBetweenRoundResistDmgMR(ByVal Index As Integer, ByVal iData As Integer)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 182 — `m_nBetweenRoundResistDmgMR(Index) = iData`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 420 — `Dim nDR_DamageResisted As Currency, nMR_Reduction As Currency`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 642 — `If m_nAtkMRdmgResist(nAttack) = 1 And nDamage > 0 Then`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 87 — `-FIX: Spells with ability 144-NonMagicalSpell (hidden in MME due to prevalence) will no longer show as Damage(-MR)`
- `MonsterSim` priority 5 — `MonsterSim/MajorMUD-PHP-Monster-Sim-master/monster.php` line 167 — `<label for="character_mr">MR:</label>`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
