# Monster attack data encoding

**Mechanic ID:** `NPC.ATTACK_DATA`  
**Bucket:** NPC AI  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Attack list encoding, multiple attacks, damage values, and special attack flags.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 3915
- Representative evidence entries: 60
- Source/path coverage: 14

## Representative Evidence

- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMain.bas` line 3186 — `Call AddMonsterDamageOutText(DetailLV, sHeader, nDamageOut & "/round (" & tAttack.sAttackDesc & ")" & sImmuTXT & sBackstabText, tAttack.sAttackDetail, _`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMain.bas` line 3275 — `Call AddMonsterDamageOutText(DetailLV, sHeader, nDamageOut & "/round (" & tAttack.sAttackDesc & ")" & sImmuTXT & sBackstabText, tAttack.sAttackDetail, _`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 54 — `-UP: Added average monster damage and true attack percentages (unique, calculated fields for MME)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 290 — `Dim nEnergyForAttack As Long, nEnergyRemaining As Long, nDamagePerEnergy As Currency`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 351 — `nDamage = 0: nMaxDamage = 0: nEnergyForAttack = 0: nMaxDPE = 0`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 420 — `Dim nDR_DamageResisted As Currency, nMR_Reduction As Currency`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 642 — `If m_nAtkMRdmgResist(nAttack) = 1 And nDamage > 0 Then`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 643 — `nMR_Reduction = Round(CalcResistedDamage(nDamage, m_nUserMR, m_nUserAntiMagic))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 645 — `'    m_nStatAtkDmgResisted(nAttack) = m_nStatAtkDmgResisted(nAttack) + (nDamage - nMR_Reduction)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 679 — `'m_nStatAtkDmgResisted(nAttack) = m_nStatAtkDmgResisted(nAttack) + nDamage`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 693 — `nDR_DamageResisted = m_nUserDR '(m_nUserDR / 2)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 694 — `If nDR_DamageResisted > nDamage Then nDR_DamageResisted = nDamage`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 695 — `m_nStatAtkDmgResisted(nAttack) = m_nStatAtkDmgResisted(nAttack) + nDR_DamageResisted`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 696 — `nDamage = nDamage - nDR_DamageResisted`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 352 — `Dim nEnergyForAttack As Long, nEnergyRemaining As Long, nDamagePerEnergy As Currency`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 413 — `nDamage = 0: nMaxDamage = 0: nEnergyForAttack = 0: nMaxDPE = 0`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 482 — `Dim nDR_DamageResisted As Currency, nMR_Reduction As Currency, nResist_Reduction As Currency`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 720 — `If m_nAtkMRdmgResist(nAttack) = 1 And nDamage > 0 Then`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 721 — `nMR_Reduction = Round(CalcResistedDamage(nDamage, m_nUserMR, m_nUserAntiMagic))`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 723 — `'    m_nStatAtkDmgResisted(nAttack) = m_nStatAtkDmgResisted(nAttack) + (nDamage - nMR_Reduction)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 730 — `Case 0: If m_nUserRCOL > 0 Then nResist_Reduction = (nDamage * (m_nUserRCOL / 100))  'col`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 731 — `Case 1: If m_nUserRFIR > 0 Then nResist_Reduction = (nDamage * (m_nUserRFIR / 100))  'fir`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 732 — `Case 2: If m_nUserRSTO > 0 Then nResist_Reduction = (nDamage * (m_nUserRSTO / 100))  'sto`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 733 — `Case 3: If m_nUserRLIT > 0 Then nResist_Reduction = (nDamage * (m_nUserRLIT / 100))  'lit`
- `MonsterSim` priority 5 — `MonsterSim/MajorMUD-PHP-Monster-Sim-master/capture_parse.php` line 42 — `$i++; $attacks[$i][1] = 'BITE'; $attacks[$i][2] = 'The SOURCE bites you for DMG damage!'; $attacks[$i][3] = 'The SOURCE lunges at you';`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
