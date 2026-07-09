# Combat operation sequence

**Mechanic ID:** `COMBAT.SEQUENCE`  
**Bucket:** Combat  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
The ordered pipeline from target selection through hit, defenses, damage, reductions, and final HP change.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 10089
- Representative evidence entries: 60
- Source/path coverage: 26

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 421 — `Dim bAttackHit As Boolean, bDodged As Boolean, bGlanced As Boolean, bResisted As Boolean`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 423 — `Dim nRoll_AttackChance As Integer, nRoll_HitChance As Integer, nRoll_DodgeChance As Integer, nAdjustedDodgeValue As Integer`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 577 — `bAttackHit = False: bDodged = False: bGlanced = False: bResisted = False`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 606 — `bAttackHit = False: bDodged = True`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 622 — `bAttackHit = False: bDodged = True`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonsterAttackSim.frm` line 5056 — `& "This is because dodge is checked after the AC check and determined to be a hit. This is compounded by different mobs with different accuracy causing the reported dodge value to fluctuate." _`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonsterAttackSim.frm` line 5297 — `'ElseIf clsMonAtkSim.nStatAtkHits(x) > 0 Or clsMonAtkSim.nStatAtkAttemptDodgedOrResisted(x) > 0 Then`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonsterAttackSim.frm` line 5300 — `'txtStatResistDodge(x).Text = Round(clsMonAtkSim.nStatAtkAttemptDodgedOrResisted(x) / (clsMonAtkSim.nStatAtkHits(x) + clsMonAtkSim.nStatAtkAttemptDodgedOrResisted(x)), 3) * 100`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 483 — `Dim bAttackHit As Boolean, bDodged As Boolean, bGlanced As Boolean, bResisted As Boolean`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 485 — `Dim nRoll_AttackChance As Integer, nRoll_HitChance As Integer, nRoll_DodgeChance As Integer, nAdjustedDodgeValue As Long`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 646 — `bAttackHit = False: bDodged = False: bGlanced = False: bResisted = False`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 672 — `bAttackHit = False: bDodged = True`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 700 — `bAttackHit = False: bDodged = True`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmHitCalc.frm` line 1022 — `nDefense = CalculateAttackDefense(nAccy, nAC, nDodge, nBSdefense, nProtEv, 0, nPerception, _`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMonsterAttackSim.frm` line 1900 — `'ElseIf clsMonAtkSimThisForm.nStatAtkHits(x) > 0 Or clsMonAtkSimThisForm.nStatAtkAttemptDodgedOrResisted(x) > 0 Then`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMonsterAttackSim.frm` line 1903 — `'txtStatResistDodge(x).Text = Round(clsMonAtkSimThisForm.nStatAtkAttemptDodgedOrResisted(x) / (clsMonAtkSimThisForm.nStatAtkHits(x) + clsMonAtkSimThisForm.nStatAtkAttemptDodgedOrResisted(x)), 3) * 100`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMonsterAttackSim.frm` line 1925 — `& "This is because dodge is checked after the AC check and determined to be a hit.  This is compounded by different mobs with different accuracy causing the reported dodge value to fluctuate." _`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMain.bas` line 3148 — `tBackStab = CalculateAttack(tBackStabProfile, a4_Surprise, nTemp, False, nSpeedAdj, nCalcDamageAC, nCalcDamageDR, nCalcDamageDodge, , , , , nBSdefense)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMain.bas` line 3165 — `tAttack = CalculateAttack(tForcedCharProfile, a6_Bash, nGlobalCharWeaponNumber(0), False, nSpeedAdj, nCalcDamageAC, nCalcDamageDR, nCalcDamageDodge)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMain.bas` line 3167 — `tAttack = CalculateAttack(tForcedCharProfile, a7_Smash, nGlobalCharWeaponNumber(0), False, nSpeedAdj, nCalcDamageAC, nCalcDamageDR, nCalcDamageDodge)`
- `MonsterSim` priority 5 — `MonsterSim/MajorMUD-PHP-Monster-Sim-master/monster.php` line 202 — `$total_damage = 0; $total_attacks = 0; $total_dodge = 0; $total_resisted = 0;`
- `MonsterSim` priority 5 — `MonsterSim/MajorMUD-PHP-Monster-Sim-master/monster.php` line 253 — `if ($dodge_chance <= $character_dodge) { $attack_hit = false; $dodged = true; $attacks[$attack_num]['attempt_dodged_resisted']++; $total_dodge++; }`
- `MonsterSim` priority 5 — `MonsterSim/MajorMUD-PHP-Monster-Sim-master/monster.php` line 388 — `($attack['type'] == 'spell' ? $attack['attempted'] : ($attack['hits'] + $attack['attempt_dodged_resisted']) )`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 16 — `-UP: update attack sim to allow for true dodge value (as computed in MME v1.9.2) or megamud's value`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 37 — `Private m_nStatAtkAttemptDodgedOrResisted(0 To 4) As Currency`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
