# HP/MP gain and regeneration

**Mechanic ID:** `CHAR.HP_MP_GAIN`  
**Bucket:** Character Progression  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Hit point/mana gain, regeneration, and derived pools.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 4908
- Representative evidence entries: 60
- Source/path coverage: 23

## Representative Evidence

- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmPasteChar.frm` line 2492 — `txtPastePartyRegenHP(iChar).Text = CalcRestingRate(nLevel(iChar), nHealth(iChar), nPlusRegen(iChar), False)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmPasteChar.frm` line 2493 — `txtPastePartyRestHP(iChar).Text = CalcRestingRate(nLevel(iChar), nHealth(iChar), nPlusRegen(iChar), True)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modExpPerHour.bas` line 921 — `nWalkSpeed, nCharHP, nCharHPRegen, nCharMana, nCharMPRegen, _`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseImport.frm` line 2850 — `Monsterrec.HPRegen = tabMonsters.Fields("HP Regen")`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/exp-per-hour-models.md` line 44 — `passive HP = `HPRegen/3` per 30s; rest HP = `HPRegen` per 20s. Mana`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 27219 — `Case 2: 'Cleric/Warlock: 2/0 ac, 5 mana regen, 3 encum, 50 max hp`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 27806 — `Case 2: 'Cleric/Warlock: 2/0 ac, 5 mana regen, 3 encum, 50 max hp`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 27815 — `Case 3: 'Priest/Mage/Druid: 50 sc, 10 mana regen, 50 max hp`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 27822 — `Case 4: 'Missionary/Bard/Gypsy: 10 bs min/bs max, 5 mana regen, 10 stealth, 50 max hp`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 35299 — `sTemp = AutoAppend(sTemp, "145|5", ",") 'mana regen`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 35304 — `sTemp = AutoAppend(sTemp, "145|10", ",") 'mana regen`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 35309 — `sTemp = AutoAppend(sTemp, "145|5", ",") 'mana regen`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 35818 — `'2: 'Cleric/Warlock: 2/0 ac, 5 mana regen, 3 encum, 50 max hp`
- `MonsterSim` priority 5 — `MonsterSim/MajorMUD-PHP-Monster-Sim-master/capture_parse.php` line 57 — `$attacks[$i][3] = 'SOURCE attempted to cast '.$long_name.' at you, but failed.';`
- `MonsterSim` priority 5 — `MonsterSim/MajorMUD-PHP-Monster-Sim-master/capture_parse.php` line 64 — `$attacks[$i][3] = 'SOURCE attempted to cast hellstorm, but failed.';`
- `MonsterSim` priority 5 — `MonsterSim/MajorMUD-PHP-Monster-Sim-master/capture_parse.php` line 69 — `$i++; $between_rounds[$i][0] = 'CHAINS'; $between_rounds[$i][1] = 'Glowing silver manacles clamp around your wrists!';`
- `MonsterSim` priority 5 — `MonsterSim/MajorMUD-PHP-Monster-Sim-master/monster.php` line 374 — `<td><span class="magenta bold"><?php echo (round($attack['attempted']/$total_attacks, 3)*100); ?>%</span></td><!-- True Cast -->`
- `MonsterSim` priority 5 — `MonsterSim/MajorMUD-PHP-Monster-Sim-master/monster.php` line 472 — `p(damage('Average Damage/'.($attack['type'] == 'spell' ? (($attack['attempted'] > $attack['hits']) ? 'Cast (including fails)':'Cast') : 'Swing').': '.round($attack['total_damage']/$attack['attempted'],2)));`
- `MonsterSim` priority 5 — `MonsterSim/MajorMUD-PHP-Monster-Sim-master/monster.php` line 473 — `p('Average '.($attack['type'] == 'spell' ? 'Casts' : 'Swings').'/Round: '.round($attack['attempted']/$number_of_rounds,2));`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 910 — `'little complex here - block 1448 casts spell 614, which gives +0 charm for`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 11 — `-UP: completely reworked script/lair value calculations to be more based on the lair composition of all monsters that regen in the lair.  lair table now created during mme export with lair information (avg damage, exp, max regen, etc) to be used in MME.  monsters and lairs have pre-calculated script values.`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 26 — `-NEW: Tools > Monsters > Item drop percentage modifier - alter monster drop percentages based on monster and item info.  this will allow you to do things like set the minimum drop percentage to 50% for items dropped by monsters that regen greater than 7 days, for example`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/changelog.txt` line 9 — `-UP: completely reworked script/lair value calculations to be more based on the lair composition of all monsters that regen in the lair.  lair table now created during mme export with lair information (avg damage, exp, max regen, etc) to be used in MME.  monsters and lairs have pre-calculated script values.`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/changelog.txt` line 24 — `-NEW: Tools > Monsters > Item drop percentage modifier - alter monster drop percentages based on monster and item info.  this will allow you to do things like set the minimum drop percentage to 50% for items dropped by monsters that regen greater than 7 days, for example.`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 4482 — `ts.Write ("Number" & vbTab & "Name" & vbTab & "Experience" & vbTab & "Index" & vbTab & "Weapon" & vbTab & "AC" & vbTab & "DR" & vbTab & "Follow" & vbTab & "MR" & vbTab & "HPs" & vbTab & "BS Defense" & vbTab & "Energy" & vbTab & "HP Regen" & vbTab & "Game Limit" & vbTab & "Active" & vbTab & "Type" & vbTab & "Alignment" & vbTab & "Gender" & vbTab & "Group" & vbTab & "Regen Time" & vbTab)`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
