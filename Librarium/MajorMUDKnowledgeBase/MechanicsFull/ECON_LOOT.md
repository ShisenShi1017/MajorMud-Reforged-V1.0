# Loot/treasure generation

**Mechanic ID:** `ECON.LOOT`  
**Bucket:** Economy and Shops  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Monster drops, treasure tables, gold generation, and item reward behavior.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.

## Evidence Coverage
- Corpus matches: 382
- Representative evidence entries: 36
- Source/path coverage: 10

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Log_MonItemPct.txt` line 4 — `LOG_ONLY: Monster #52 - giant spider: Item #216 - gold ring: Drop % changed from 20 to 50`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Log_MonItemPct.txt` line 5 — `LOG_ONLY: Monster #59 - Thrag: Item #211 - golden belt: Drop % changed from 20 to 50`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Log_MonItemPct.txt` line 13 — `LOG_ONLY: Monster #69 - chest: Item #265 - golden broadsword: Drop % changed from 20 to 50`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Log_MonItemPct.txt` line 16 — `LOG_ONLY: Monster #122 - high druid: Item #460 - golden sickle: Drop % changed from 20 to 50`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Log_MonItemPct.txt` line 71 — `LOG_ONLY: Monster #588 - manscorpion king: Item #1170 - golden pike: Drop % changed from 20 to 50`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Log_MonItemPct.txt` line 96 — `LOG_ONLY: Monster #658 - massive cocoon: Item #642 - white gold ring: Drop % changed from 20 to 50`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Log_MonItemPct.txt` line 98 — `LOG_ONLY: Monster #700 - dark phoenix: Item #956 - golden chest: Drop % changed from 20 to 50`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/_outputsmashuptemp.txt` line 215 — `19 gold drop to the ground.`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/_outputsmashuptemp.txt` line 436 — `1 gold drop to the ground.`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Log_MonItemPct.txt` line 1 — `Monster Item Drop Percentage Modifier 8/5/2021 @ 11:11:49 AM`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Log_MonItemPct.txt` line 23 — `LOG_ONLY: Monster #174 - dark bishop: Item #454 - hell-flail: Drop % changed from 20 to 50`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Log_MonItemPct.txt` line 24 — `LOG_ONLY: Monster #174 - dark bishop: Item #1708 - runed sacrificial robes: Drop % changed from 20 to 50`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Log_MonItemPct.txt` line 52 — `LOG_ONLY: Monster #346 - captain of the guard: Item #826 - obsidian field plate: Drop % changed from 20 to 50`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 36 — `9046|0|0|flag checks and class rewards`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/ExportedModules/Majorbbs/sprintf_Tests.cs` line 43 — `[InlineData("%-8s", "gold crowns", "gold crowns")]`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/ExportedModules/Majorbbs/sprintf_Tests.cs` line 44 — `[InlineData("%-8s", "gold    ", "gold")]`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Database/Repositories/Account/AccountRepository.cs` line 30 — `public bool DropTable()`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Database/Repositories/Account/AccountRepository.cs` line 32 — `var result = Query(EnumQueries.DropAccountsTable, null);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Database/Repositories/Account/AccountRepository.cs` line 93 — `DropTable();`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Database/Repositories/Account/IAccountRepository.cs` line 10 — `bool DropTable();`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Database/Repositories/Account/Queries/DropAccountsTable.sql` line 1 — `﻿DROP TABLE Accounts;`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Database/Repositories/Account/Queries/EnumQueries.cs` line 20 — `[SqlQuery("DropAccountsTable.sql")]`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Database/Repositories/Account/Queries/EnumQueries.cs` line 21 — `[Description("Drops the Account Table")]`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Database/Repositories/Account/Queries/EnumQueries.cs` line 22 — `DropAccountsTable,`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Database/Repositories/AccountKey/AccountKeyRepository.cs` line 25 — `public bool DropTable()`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
