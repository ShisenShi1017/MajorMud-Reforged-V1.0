# Banking and money storage

**Mechanic ID:** `ECON.BANKING`  
**Bucket:** Economy and Shops  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Deposit, withdraw, banked gold, and related persistence.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.

## Evidence Coverage
- Corpus matches: 474
- Representative evidence entries: 27
- Source/path coverage: 9

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Log_MonItemPct.txt` line 4 — `LOG_ONLY: Monster #52 - giant spider: Item #216 - gold ring: Drop % changed from 20 to 50`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Log_MonItemPct.txt` line 5 — `LOG_ONLY: Monster #59 - Thrag: Item #211 - golden belt: Drop % changed from 20 to 50`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Log_MonItemPct.txt` line 13 — `LOG_ONLY: Monster #69 - chest: Item #265 - golden broadsword: Drop % changed from 20 to 50`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Log_MonItemPct.txt` line 16 — `LOG_ONLY: Monster #122 - high druid: Item #460 - golden sickle: Drop % changed from 20 to 50`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Log_MonItemPct.txt` line 71 — `LOG_ONLY: Monster #588 - manscorpion king: Item #1170 - golden pike: Drop % changed from 20 to 50`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Log_MonItemPct.txt` line 96 — `LOG_ONLY: Monster #658 - massive cocoon: Item #642 - white gold ring: Drop % changed from 20 to 50`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Log_MonItemPct.txt` line 98 — `LOG_ONLY: Monster #700 - dark phoenix: Item #956 - golden chest: Drop % changed from 20 to 50`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 539 — `425|0|426|GOLD command given to nasty looking man`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/Nightmare Redux.vbp` line 64 — `Form=frmBank.frm`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/Nightmare Redux.vbw` line 52 — `frmBank = 0, 0, 0, 0, C, 260, 260, 1424, 1106, C`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 200 — `-NEW: Find/Find Next for bank editor`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 241 — `-NEW: shops now show bank account/cost info for gangshops`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/ExportedModules/Majorbbs/sprintf_Tests.cs` line 43 — `[InlineData("%-8s", "gold crowns", "gold crowns")]`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/ExportedModules/Majorbbs/sprintf_Tests.cs` line 44 — `[InlineData("%-8s", "gold    ", "gold")]`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/scripts/mmud_edit.py` line 40 — `# gold low-byte @ 0x607`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 26 — `-UP: Tolls in room tooltips will now show as gold or higher currency`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/_outputsmashuptemp.txt` line 11 — `You are carrying 28 gold crowns, starsteel plate gauntlets (Hands), golden`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/_outputsmashuptemp.txt` line 16 — `nexus spear (Weapon Hand), griffon shield, king crab claw, white gold ring,`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/_outputsmashuptemp.txt` line 20 — `obsidian key, 2 golden idols, skeleton key, steel key, 2 black star keys,`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/_outputsmashuptemp.txt` line 35 — `You are carrying 1 platinum piece, 5 gold crowns, white gold ring (Finger),`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/_outputsmashuptemp.txt` line 39 — `slippers (Feet), moonstone ring (Finger), golden chalice (Off-Hand), lionskin`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/_outputsmashuptemp.txt` line 40 — `belt (Waist), amber sceptre (Weapon Hand), silverbark canoe, mine pass, golden`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/_outputsmashuptemp.txt` line 60 — `leather gauntlets (Hands), platinum ring (Finger), white gold ring (Finger),`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/_outputsmashuptemp.txt` line 61 — `golden belt (Waist), rigid leather pants (Legs), hardened leather boots`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/_outputsmashuptemp.txt` line 102 — `You are carrying 4 runic coins, 71 platinum pieces, 99 gold crowns, 5 silver`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
