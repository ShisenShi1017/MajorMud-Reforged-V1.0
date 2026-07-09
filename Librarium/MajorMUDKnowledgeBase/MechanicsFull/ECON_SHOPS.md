# Shop inventory and behavior

**Mechanic ID:** `ECON.SHOPS`  
**Bucket:** Economy and Shops  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Shop stock, restrictions, refresh, and buy/sell interaction rules.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.

## Evidence Coverage
- Corpus matches: 2491
- Representative evidence entries: 36
- Source/path coverage: 17

## Representative Evidence

- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 218 — `-NEW: You can also paste items from your inventory or from seen on the ground and mark them as buy, sell, drop, hide pickup, or stash.`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 371 — `-UP: shops will now show the appropriate coin type value next to the copper value when buying, selling, training`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 124 — `-NEW: You can also paste items from your inventory or from seen on the ground and mark them as buy, sell, drop, hide pickup, or stash.`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 278 — `-UP: shops will now show the appropriate coin type value next to the copper value when buying, selling, training`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.txt` line 206 — `-NEW: You can also paste items from your inventory or from seen on the ground and mark them as buy, sell, drop, hide pickup, or stash.`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 22379 — `& "Doing so would allow you to sort the list by the shops you intended to sell/buy the items.", vbInformation`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modItemParse.bas` line 41 — `'        If no BUY, chooses SELL-only; tie?lowest shop #.`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modItemParse.bas` line 42 — `'        Returns friendly buy/sell or (sell) X text and the chosen shop #.`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modItemParse.bas` line 132 — `bNObuy     As Boolean  ' True means shop does not sell to player (sell-only)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modItemParse.bas` line 1056 — `' score by cheapest BUY if any shop buys; else cheapest SELL`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modItemParse.bas` line 1238 — `' BUY is preferred; ties -> lower shop #. If no BUY exists, pick best SELL (ties -> lower shop #).`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modItemParse.bas` line 1240 — `' chooser: prefers BUY when available; else SELL-only; ties -> lower shop #`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 2957 — `Call MarkItemInGame(Shoprec.ShopItemNumber(x), "Shop(sell) #" & Shoprec.Number, True)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Log_MonItemPct.txt` line 23 — `LOG_ONLY: Monster #174 - dark bishop: Item #454 - hell-flail: Drop % changed from 20 to 50`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Log_MonItemPct.txt` line 24 — `LOG_ONLY: Monster #174 - dark bishop: Item #1708 - runed sacrificial robes: Drop % changed from 20 to 50`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 739 — `--Bishop Quest`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 740 — `'Bishop (1/525)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 741 — `217|0|0|bishop's greet text`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/Nightmare Redux.vbp` line 19 — `Form=frmShop.frm`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/Nightmare Redux.vbw` line 7 — `frmShop = 208, 208, 2073, 992, C, 208, 208, 1372, 1054, C`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 96 — `-BUGFIX: ‘per item’ ‘only if’ on uni mod for monsters and shops`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 175 — `-UP: added 'only if markup' for shops`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 198 — `-NEW: enhanced the item find to now search users and shops too`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 202 — `-NEW: map now marks room spells/shops/exit & death rooms`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/LICENSE` line 8 — `to use, copy, modify, merge, publish, distribute, sublicense, and/or sell`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
