# Item field schema

**Mechanic ID:** `ITEM.FIELD_SCHEMA`  
**Bucket:** Items and Equipment  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Item data columns, binary fields, flags, and editor interpretation.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 24639
- Representative evidence entries: 60
- Source/path coverage: 18

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/modFieldmaps_vO.bas` line 31 — `HPBonus As Integer      'HP00    -   HP Bonus as Integer      2     46      10`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/modFieldmaps_vO.bas` line 656 — `AbilityModifier(29) As Integer      '60     1970   722 (30 elements × 2 bytes each = 60 bytes)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/modFieldmaps_vO.bas` line 663 — `unknown13g As Integer               '2      1984   729  TOTAL "DefenseModifier" FROM ability 104`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/modFieldmaps_vO.bas` line 1721 — `AddField Map, ctr, FLD_INTEGER, 2       'HP00    -   HP Bonus as Integer      2`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/Fsd/EnumFsdFieldType.cs` line 1 — `﻿namespace MBBSEmu.HostProcess.Fsd`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/Fsd/FsdFieldSpec.cs` line 3 — `namespace MBBSEmu.HostProcess.Fsd`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/Structs/EnumFsdfldFlags.cs` line 3 — `namespace MBBSEmu.HostProcess.Structs`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modItemParse.bas` line 1859 — `Call AddOneRow(lv, hit, sSource, hit.encum, nQTY, bestShopName, valueCell, sortTagCopper, (hit.ItemType = 7), sFlag)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 498 — `9537|0|9483|death spell of dark mage -- flag check, give 175 mill exp bonus`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 503 — `9547|0|0|death spell connected to text block -- flag check, add 50 mill exp bonus`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 493 — `-NEW: universal modifier --perform math functions on many of the fields within the editors`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/changelog.txt` line 518 — `-NEW: universal modifier --perform math functions on many of the fields within the`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/nmr tools.txt` line 38 — `---This very useful tool allows you to change ranges of specific fields in the room database and is  more configurable for rooms than the universal modifier. Very useful for fixing area regen problems, or changing the room description of an entire area. Could also be used to remove large ranges from rooms, such as setting every room's light setting to 0.`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 4131 — `tabRaces.Fields("HP Bonus") = Racerec.HPBonus`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseImport.frm` line 2536 — `Racerec.HPBonus = tabRaces.Fields("HP Bonus")`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 5216 — `tabRaces.Fields("HPPerLVL") = Racerec.HPBonus`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveFileProcessor_Tests.cs` line 31 — `public class BtrieveFileProcessor_Tests : TestBase, IDisposable`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveFileProcessor_Tests.cs` line 43 — `public BtrieveFileProcessor_Tests()`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveFileProcessor_Tests.cs` line 74 — `var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveFileProcessor_Tests.cs` line 129 — `var connectionString = BtrieveFileProcessor.GetDefaultConnectionStringBuilder(fullPath).ToString();`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveFileProcessor_Tests.cs` line 134 — `stmt.ExecuteScalar().Should().Be(BtrieveFileProcessor.CURRENT_VERSION);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveFileProcessor_Tests.cs` line 167 — `using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveFileProcessor_Tests.cs` line 222 — `using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveFileProcessor_Tests.cs` line 250 — `using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveFileProcessor_Tests.cs` line 278 — `using var btrieve = new BtrieveFileProcessor(serviceResolver.GetService<IFileUtility>(), _modulePath, "MBBSEMU.DAT", CACHE_SIZE);`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
