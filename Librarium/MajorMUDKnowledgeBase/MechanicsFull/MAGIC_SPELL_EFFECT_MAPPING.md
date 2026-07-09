# Spell effect mapping

**Mechanic ID:** `MAGIC.SPELL_EFFECT_MAPPING`  
**Bucket:** Magic  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Mapping spell IDs/ability fields to damage, buff, debuff, healing, summon, and utility effects.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 6319
- Representative evidence entries: 60
- Source/path coverage: 14

## Representative Evidence

- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas` line 1004 — `If CalculateSpellCast.nManaCost > 0 And CalculateSpellCast.nManaCost <= tCharStats.nMaxMana Then  'and (CalculateSpellCast.bDoesDamage Or CalculateSpellCast.bDoesHeal)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas` line 1013 — `If CalculateSpellCast.bDoesDamage Or CalculateSpellCast.bDoesHeal Then`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas` line 1016 — `If CalculateSpellCast.bDoesDamage And CalculateSpellCast.bDoesHeal Then`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas` line 1017 — `sAvgRound = sCastLVL & IIf(nSpellDuration > 1, nSpellAvgCast, CalculateSpellCast.nAvgRoundDmg) & " damage + " & CalculateSpellCast.nAvgRoundHeals & " heals"`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas` line 1023 — `If (CalculateSpellCast.bDoesDamage Or CalculateSpellCast.bDoesHeal) Then`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas` line 1849 — `nCount = nCount + 1 'still counting here because its presence would reduce the chance of casting the other spells in the group, thereby reducing their overall effect on the average damage`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMain.bas` line 4219 — `If bCalcCombat And (tSpellcast.bDoesDamage Or tSpellcast.bDoesHeal) And Len(tSpellcast.sAvgRound) > 0 Then`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMain.bas` line 4236 — `And Val(frmMain.lblCharMaxMana.Tag) > 0 And (tSpellCast.bDoesDamage Or tSpellCast.bDoesHeal) And tSpellCast.nDuration = 1`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMain.bas` line 4315 — `'If tChar.nSpellDmgBonus > 0 And (tSpellcast.bDoesDamage Or (tSpellcast.bDoesHeal And bGreaterMUD)) Then`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMain.bas` line 5496 — `nSpellDamage = Round(tSpellcast.nAvgRoundHeals / nManaCost, 1)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/mme_export_notes.txt` line 24 — `masks spells cast (ability 43-CastSP) by the items as in game`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 1876 — `clsMonAtkSim.nAtkMin(x) = GetSpellMinDamage(Monsterrec.AttackAccuSpell(x), Monsterrec.AttackMaxHCastLvl(x))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 1877 — `clsMonAtkSim.nAtkMax(x) = GetSpellMaxDamage(Monsterrec.AttackAccuSpell(x), Monsterrec.AttackMaxHCastLvl(x))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 1888 — `clsMonAtkSim.nAtkMin(x) = GetSpellMinDamage(Monsterrec.AttackAccuSpell(x), Monsterrec.AttackMaxHCastLvl(x))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 1889 — `clsMonAtkSim.nAtkMax(x) = GetSpellMaxDamage(Monsterrec.AttackAccuSpell(x), Monsterrec.AttackMaxHCastLvl(x))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 1900 — `clsMonAtkSim.nAtkMin(x) = GetSpellMinDamage(Monsterrec.AttackAccuSpell(x), Monsterrec.AttackMaxHCastLvl(x))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 1901 — `clsMonAtkSim.nAtkMax(x) = GetSpellMaxDamage(Monsterrec.AttackAccuSpell(x), Monsterrec.AttackMaxHCastLvl(x))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 1959 — `clsMonAtkSim.nBetweenRoundMin(x) = GetSpellMinDamage(Monsterrec.SpellNumber(x), Monsterrec.SpellCastLvl(x))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 1960 — `clsMonAtkSim.nBetweenRoundMax(x) = GetSpellMaxDamage(Monsterrec.SpellNumber(x), Monsterrec.SpellCastLvl(x))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 1971 — `clsMonAtkSim.nBetweenRoundMin(x) = GetSpellMinDamage(Monsterrec.SpellNumber(x), Monsterrec.SpellCastLvl(x))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 1972 — `clsMonAtkSim.nBetweenRoundMax(x) = GetSpellMaxDamage(Monsterrec.SpellNumber(x), Monsterrec.SpellCastLvl(x))`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 1983 — `clsMonAtkSim.nBetweenRoundMin(x) = GetSpellMinDamage(Monsterrec.SpellNumber(x), Monsterrec.SpellCastLvl(x))`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 93 — `-FIX: Area effect spells cast by monsters utilizing abil-1 being marked as invalid for [GreaterMUD] (they ARE still invalid for stock)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 263 — `-UP: Adding healing average and healing/mana colums like there were already for damage`
- `MonsterSim` priority 5 — `MonsterSim/MajorMUD-PHP-Monster-Sim-master/capture_parse.php` line 61 — `$i++; $attacks[$i][1] = 'HEAL'; $attacks[$i][2] = 'The room casts room heal on you, healing DMG damage!'; $attacks[$i][3] = 'AAAAAAA';`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
