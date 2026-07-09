# Dodge evaluation order

**Mechanic ID:** `COMBAT.DODGE_ORDER`  
**Bucket:** Combat  
**Status:** open_conflict  
**Runtime:** not_changed_by_phase2_population

## Description
Whether dodge is checked before AC, after AC, or through mode-specific branches.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 1320
- Representative evidence entries: 60
- Source/path coverage: 11

## Notes
- Conflict carried forward: order/mode of dodge versus AC requires exact pipeline resolution.

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 16 — `-UP: update attack sim to allow for true dodge value (as computed in MME v1.9.2) or megamud's value`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 37 — `Private m_nStatAtkAttemptDodgedOrResisted(0 To 4) As Currency`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 47 — `Private m_nUserDodge As Long`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 50 — `Private m_bDodgeBeforeAC As Boolean`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 97 — `Public Property Let bDodgeBeforeAC(ByVal vData As Boolean)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 98 — `m_bDodgeBeforeAC = vData`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 101 — `Public Property Get bDodgeBeforeAC() As Boolean`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 102 — `bDodgeBeforeAC = m_bDodgeBeforeAC`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 421 — `Dim bAttackHit As Boolean, bDodged As Boolean, bGlanced As Boolean, bResisted As Boolean`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 423 — `Dim nRoll_AttackChance As Integer, nRoll_HitChance As Integer, nRoll_DodgeChance As Integer, nAdjustedDodgeValue As Integer`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 577 — `bAttackHit = False: bDodged = False: bGlanced = False: bResisted = False`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 602 — `'this will allow the dodge percentage to match what players see in megamud`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 129 — `-FIX: Updated dodge formula for mob v player damage calc in [GreaterMUD]`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 158 — `-UP: Hit calc will now show dodge/overall always`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 188 — `-NEW: Dodge formula discovered and now calculated automatically`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 236 — `-UP: Attack sim updated with new understanding of dodge calculation and how it's factored into physical attacks`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 237 — `-UP: Added option to attack sim to calculate dodge based on actual dodge (new calculation) or what is shown in megamud (value seen in megamud is only accurate if you're fighting mobs with all the same accuracy on their physical attacks)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 39 — `Private m_nStatAtkAttemptDodgedOrResisted(0 To 4) As Currency`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 49 — `Private m_nUserDodge As Long`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 52 — `Private m_bDodgeBeforeAC As Boolean`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 101 — `Private m_DODGE_SOFTCAP As Integer`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 102 — `Private m_DODGE_CAP As Integer`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 138 — `Public Property Get DODGE_SOFTCAP() As Integer`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 139 — `DODGE_SOFTCAP = m_DODGE_SOFTCAP`
- `MonsterSim` priority 5 — `MonsterSim/MajorMUD-PHP-Monster-Sim-master/monster.php` line 27 — `$attacks[$i]['attempt_dodged_resisted'] = 0;`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
