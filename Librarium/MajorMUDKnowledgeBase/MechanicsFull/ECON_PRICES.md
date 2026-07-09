# Price/value formula

**Mechanic ID:** `ECON.PRICES`  
**Bucket:** Economy and Shops  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Item value, buy/sell price, markup, markdown, and economic modifiers.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 3890
- Representative evidence entries: 41
- Source/path coverage: 20

## Representative Evidence

- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 371 — `-UP: shops will now show the appropriate coin type value next to the copper value when buying, selling, training`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 659 — `-NEW: cost shown for buying/selling/training`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 278 — `-UP: shops will now show the appropriate coin type value next to the copper value when buying, selling, training`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 566 — `-NEW: cost shown for buying/selling/training`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modItemParse.bas` line 1213 — `selectMetric = EvaluateBestPriceForHit(hit, nCharm, bestShopName, valueCell, moreShops, sellOnly, chosenShopNum)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modItemParse.bas` line 1312 — `valueCell = itemVal.sFriendlyBuyShort & " / " & itemVal.sFriendlySellShort`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modItemParse.bas` line 1663 — `'     sValueCell       -> "buy/sell" or "(sell) X" like the original`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudDatabase.bas` line 3473 — `Dim nBaseCost As Double, nCharmModBuy As Double, nCharmModSell As Double, nCopperBuy As Double, nCopperSell As Double`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMain.bas` line 1880 — `If tValue.nBaseCost > 0 And Len(tValue.sFriendlyBuyShortGanghouse) > 0 Then`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/notes.txt` line 197 — `.Columns.Append "Cost", adInteger			==> Price`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmRecordChange.frm` line 3827 — `'price <value> <msg>`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmUniversalModifier.frm` line 950 — `cmbOnlyIfAuxValue(Index).AddItem "Gold"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/modMMUDFunctions.bas` line 2266 — `Case 2: GetCostType = "Gold"`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 89 — `-FIX: Sell value formula in [GreaterMUD]`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 218 — `-NEW: You can also paste items from your inventory or from seen on the ground and mark them as buy, sell, drop, hide pickup, or stash.`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 10 — `-FIX: Sell value formula in [GreaterMUD]`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Log_MonItemPct.txt` line 4 — `LOG_ONLY: Monster #52 - giant spider: Item #216 - gold ring: Drop % changed from 20 to 50`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Log_MonItemPct.txt` line 5 — `LOG_ONLY: Monster #59 - Thrag: Item #211 - golden belt: Drop % changed from 20 to 50`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Log_MonItemPct.txt` line 13 — `LOG_ONLY: Monster #69 - chest: Item #265 - golden broadsword: Drop % changed from 20 to 50`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Log_MonItemPct.txt` line 16 — `LOG_ONLY: Monster #122 - high druid: Item #460 - golden sickle: Drop % changed from 20 to 50`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Log_MonItemPct.txt` line 71 — `LOG_ONLY: Monster #588 - manscorpion king: Item #1170 - golden pike: Drop % changed from 20 to 50`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Log_MonItemPct.txt` line 96 — `LOG_ONLY: Monster #658 - massive cocoon: Item #642 - white gold ring: Drop % changed from 20 to 50`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Log_MonItemPct.txt` line 98 — `LOG_ONLY: Monster #700 - dark phoenix: Item #956 - golden chest: Drop % changed from 20 to 50`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 539 — `425|0|426|GOLD command given to nasty looking man`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/LICENSE` line 8 — `to use, copy, modify, merge, publish, distribute, sublicense, and/or sell`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
