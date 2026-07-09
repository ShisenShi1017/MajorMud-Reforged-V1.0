# Status effect model

**Mechanic ID:** `STATUS.EFFECTS`  
**Bucket:** Status Effects  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Poison, disease, paralysis, invisibility, protections, and other persistent effects.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.

## Evidence Coverage
- Corpus matches: 2294
- Representative evidence entries: 36
- Source/path coverage: 11

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMain.frm` line 653 — `Do While nStatus = 0 And bStopProcess = False`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMain.frm` line 786 — `Do While nStatus = 0 And bStopProcess = False`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMain.frm` line 894 — `Do While nStatus = 0 And bStopProcess = False`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMain.frm` line 1238 — `'Do While nStatus = 0 And bStopProcess = False`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMain.frm` line 1353 — `Do While nStatus = 0 And bStopProcess = False`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMain.frm` line 1495 — `Do While nStatus = 0 And bStopProcess = False`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMain.frm` line 1830 — `Do While nStatus = 0 And bStopProcess = False`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMain.frm` line 1961 — `Do While nStatus = 0 And bStopProcess = False`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMain.frm` line 2094 — `Do While nStatus = 0 And bStopProcess = False`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMain.frm` line 2168 — `Do While nStatus = 0 And bStopProcess = False`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMain.frm` line 2258 — `Do While nStatus = 0 And bStopProcess = False`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMain.frm` line 2334 — `Do While nStatus = 0 And bStopProcess = False`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/ExportedModules/Majorbbs/onsys_Tests.cs` line 57 — `ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, ONSYSN_ORDINAL, new List<ushort> { stringPointer.Offset, stringPointer.Segment, isInvisible });`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/Enums/EnumUserStatus.cs` line 1 — `namespace MBBSEmu.HostProcess.Enums`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/Galgsbl.cs` line 789 — `if (channel.DataToProcess && channel.GetStatus() == EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/Majorbbs.cs` line 321 — `//Bail if it's max value, not processing any input or status`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/Fsd/FsdStatus.cs` line 3 — `namespace MBBSEmu.HostProcess.Fsd`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/MbbsHost.cs` line 727 — `///     The registered STSROU is invoked any time there is a status change or deferred processing`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/MbbsHost.cs` line 913 — `//If BTUCHI Injected a deferred Execution Status, respect that vs. processing the input`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/CLAUDE.md` line 7 — `MBBSEmu is a cross-platform emulator for running legacy MajorBBS and Worldgroup modules (16-bit NE format DLLs) on modern systems. It emulates both the Galacticomm host environment and an x86-16 processor. Written in C# targeting .NET 10.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/CLAUDE.md` line 54 — `- **MBBSEmu/** - Main executable: emulation engine, host process, exported modules`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/CLAUDE.md` line 67 — `5. **Exported Modules** (`HostProcess/ExportedModules/`): C# implementations of MajorBBS/Worldgroup APIs that modules call via imported ordinals. Each mapped to a virtual segment:`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/CLAUDE.md` line 73 — `- **Btrieve** (`Btrieve/`): Database engine emulation backed by SQLite. `BtrieveFileProcessor` handles CRUD operations with key-based access.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/CLAUDE.md` line 93 — `- **Integration tests** (`Tests/Integration/`): Full host process tests. Extend `MBBSEmuIntegrationTestBase`.`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/CLAUDE.md` line 51 — `code. Hand-editing the control/layout block is risky; prefer editing the procedure code at`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
