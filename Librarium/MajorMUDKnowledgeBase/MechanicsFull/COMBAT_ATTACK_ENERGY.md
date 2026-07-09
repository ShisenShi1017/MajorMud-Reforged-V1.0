# Attack energy/action loop

**Mechanic ID:** `COMBAT.ATTACK_ENERGY`  
**Bucket:** Combat  
**Status:** high_confidence_verified  
**Runtime:** not_changed_by_phase2_population

## Description
How NPC/player physical actions consume energy and allow additional attacks.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 1453
- Representative evidence entries: 60
- Source/path coverage: 16

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 288 — `Dim nMaxEnergyRound As Long, nMaxCostAttack As Long`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 338 — `Do While (nLeastEnergyUsed + nMaxCostAttack) <= nMaxEnergyRound`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmSwingCalc.frm` line 1853 — `nSwings = Round((1000 / nEnergy), 4)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 350 — `Dim nMaxEnergyRound As Long, nMaxCostAttack As Long`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 400 — `Do While (nLeastEnergyUsed + nMaxCostAttack) <= nMaxEnergyRound`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmSwingCalc.frm` line 2006 — `nSwings = Round((1000 / nEnergy), 4)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas` line 1587 — `nSwings = Round(1000 / nEnergy, 4)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 287 — `Dim nLeastEnergyUsed As Long, nLowestCostAttack As Long, nLowEnergy As Long`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 315 — `nLowEnergy = Round(m_nAtkEnergy(nAttack) / 2)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 319 — `If (nLowestCostAttack = 0 Or nLowEnergy < nLowestCostAttack) And nLowEnergy > 0 Then`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 320 — `nLowestCostAttack = nLowEnergy`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 323 — `If nMaxCostAttack = 0 Or m_nAtkEnergy(nAttack) > nMaxCostAttack Then`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 324 — `nMaxCostAttack = m_nAtkEnergy(nAttack)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 337 — `nLeastEnergyUsed = nLowestCostAttack`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 339 — `nLeastEnergyUsed = nLeastEnergyUsed + nLowestCostAttack`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 342 — `If nEnergyRemaining < nLowestCostAttack Then nEnergyRemaining = nEnergyRemaining + (nLowestCostAttack - 1)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 162 — `-FIX: Rounding swings on EQ tab (will now be truncated)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 176 — `-UP: Added weapon swings and 1st round damage columns to weapon tab`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 349 — `Dim nLeastEnergyUsed As Long, nLowestCostAttack As Long, nLowEnergy As Long`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 377 — `nLowEnergy = Round(m_nAtkEnergy(nAttack) / 2)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 381 — `If (nLowestCostAttack = 0 Or nLowEnergy < nLowestCostAttack) And nLowEnergy > 0 Then`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 382 — `nLowestCostAttack = nLowEnergy`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 385 — `If nMaxCostAttack = 0 Or m_nAtkEnergy(nAttack) > nMaxCostAttack Then`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 386 — `nMaxCostAttack = m_nAtkEnergy(nAttack)`
- `MonsterSim` priority 5 — `MonsterSim/MajorMUD-PHP-Monster-Sim-master/monster.php` line 312 — `$energy_used = round($attack['energy']/2,0);`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
