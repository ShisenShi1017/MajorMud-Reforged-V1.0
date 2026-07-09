# Training and advancement costs

**Mechanic ID:** `CHAR.TRAINING`  
**Bucket:** Character Progression  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Trainer rules, advancement costs, and learned ability requirements.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.

## Evidence Coverage
- Corpus matches: 368
- Representative evidence entries: 36
- Source/path coverage: 14

## Representative Evidence

- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 659 — `-NEW: cost shown for buying/selling/training`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 566 — `-NEW: cost shown for buying/selling/training`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 761 — `4327|0|0|tele for solo trainer, class filtered`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 1242 — `Call AddToDefaultsArr(x, 1, 289, 289, "halls of training, entrance")`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 1252 — `Call AddToDefaultsArr(x, 17, 2867, 2867, "sysop training room")`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMain.frm` line 2395 — `frmProgressBar.lblCaption.Caption = "Retraining Users ..."`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmShop.frm` line 1989 — `'8=Training`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmShop.frm` line 2008 — `If cmbType.ListIndex = 8 Then 'training`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmUniversalModifier.frm` line 1158 — `cmbOnlyIfValue(Index).AddItem "Training"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/modMMUDFunctions.bas` line 153 — `Case 8: GetShopType = "Training"`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 168 — `-FIX: Trainer sorting by level`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 210 — `-FIX: Copy shop trainer to clipboard`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 330 — `-added a trainer filter to shops - shows where you can train based on character/level`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 371 — `-UP: shops will now show the appropriate coin type value next to the copper value when buying, selling, training`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 76 — `-FIX: Trainer sorting by level`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 116 — `-FIX: Copy shop trainer to clipboard`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 237 — `-added a trainer filter to shops - shows where you can train based on character/level`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.archive.txt` line 278 — `-UP: shops will now show the appropriate coin type value next to the copper value when buying, selling, training`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.txt` line 158 — `-FIX: Trainer sorting by level`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/changelog.txt` line 198 — `-FIX: Copy shop trainer to clipboard`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 757 — `4286|0|0|tele from final nahr's pit & for already-trained chars from ent to`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 241 — `-NEW: shops now show bank account/cost info for gangshops`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 275 — `-UP: shop editor now shows all items on one page along with the adjusted cost`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 321 — `-NEW: tool to give all users a retrain`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveFileProcessor_Tests.cs` line 463 — `public void InsertionConstraintFailure()`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
