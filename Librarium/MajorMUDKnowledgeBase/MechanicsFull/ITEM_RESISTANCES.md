# Item resistances

**Mechanic ID:** `ITEM.RESISTANCES`  
**Bucket:** Items and Equipment  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Resistance fields provided by equipment or consumables.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 2747
- Representative evidence entries: 60
- Source/path coverage: 25

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonsterAttackSim.frm` line 5118 — `MsgBox "Note: MR < 50 gives negative resistance when MR is taken into account.", vbInformation`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 418 — `-UP: added MR value corresponding resistance values`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 325 — `-UP: added MR value corresponding resistance values`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMonsterAttackSim.frm` line 2081 — `MsgBox "Note: MR < 50 gives negative resistance when MR is taken into account.", vbInformation`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 824 — `4271|0|4275|summon fire shard, checks casting stat`
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
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Assets/MBBSEMU.MSG` line 82 — `TMROUT {Timer has fired! %d:%d rti:%d`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 87 — `-FIX: Spells with ability 144-NonMagicalSpell (hidden in MME due to prevalence) will no longer show as Damage(-MR)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 161 — `-UP: Accy, Stealth, and MR will now be colored on the EQ tab as they are calculated stats`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 189 — `-NEW: Magic Resistance formula discovered and now calculated automatically`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 261 — `-NEW: Ability to calculate spell damage / resist based on the defender MR rating`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 384 — `-NEW: new field (from NMR v1.8+ exports) for displaying if a spell is resistible by no one, anyone, and anti-magic`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/_outputsmashuptemp.txt` line 76 — `3   6    rcol  resist cold`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/_outputsmashuptemp.txt` line 79 — `5   8    rfir  resist fire`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 33 — `Private m_nAtkMRdmgResist(0 To 4) As Integer`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 95 — `Private m_nBetweenRoundResistDmgMR(0 To 4) As Integer`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
