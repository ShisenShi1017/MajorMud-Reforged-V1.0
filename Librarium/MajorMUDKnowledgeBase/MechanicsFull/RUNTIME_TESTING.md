# Tests and validation harnesses

**Mechanic ID:** `RUNTIME.TESTING`  
**Bucket:** Runtime Support  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Test fixtures, validators, simulators, and regression tooling.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 4537
- Representative evidence entries: 60
- Source/path coverage: 11

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonsterAttackSim.frm` line 4887 — `nTest = SpellHasAbility(Monsterrec.AttackAccuSpell(x), 17) '17=damage-MR`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonsterAttackSim.frm` line 5215 — `nTest = SpellHasAbility(Val(txtBetweenSpellNumber(x).Text), 17) '17=damage-mr`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonsterAttackSim.frm` line 5638 — `CalculateItemBonuses = CalculateItemBonuses + nTest`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonsterAttackSim.frm` line 5649 — `CalculateItemBonuses = CalculateItemBonuses + (nTest * (Val(txtItemDropPer(x).Text) / 100))`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/DOS/Interrupts/Int7Bh_Tests.cs` line 879 — `// now we need to validate logical currency by stepping through based on key_number == 1, there is one previous record, and then nothing`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/DOS/Interrupts/Int7Bh_Tests.cs` line 922 — `// now we need to validate logical currency by stepping through based on key_number == 1, there is one previous record, and then nothing`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/ExportedModules/ExportedModuleTestBase.cs` line 236 — `///     address for the property. Because Unit Tests aren't going through the same relocation process, we simulate it by getting the`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 9 — `-UP: Added a new optional exp/hr model (D) by Claude - a round-by-round combat & recovery simulation (selectable in settings)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modExpPerHour.bas` line 1894 — `DebugLogPrint "No simulations found. Add lines to SIM_TABLE."`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 16 — `-UP: update attack sim to allow for true dodge value (as computed in MME v1.9.2) or megamud's value`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 53 — `-UP: Added attack names field (will show attack names on monster details and in simulator)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 4 — `Persistable = 0  'NotPersistable`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 11 — `Attribute VB_Creatable = True`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 15 — `Attribute VB_Ext_KEY = "Top_Level" ,"Yes"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 30 — `Private m_nAtkResist(0 To 4) As Integer`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 31 — `Private m_nAtkMRdmgResist(0 To 4) As Integer`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 36 — `Private m_nStatAtkDmgResisted(0 To 4) As Currency`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/CLAUDE.md` line 7 — `MBBSEmu is a cross-platform emulator for running legacy MajorBBS and Worldgroup modules (16-bit NE format DLLs) on modern systems. It emulates both the Galacticomm host environment and an x86-16 processor. Written in C# targeting .NET 10.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/CLAUDE.md` line 54 — `- **MBBSEmu/** - Main executable: emulation engine, host process, exported modules`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/CLAUDE.md` line 63 — `1. **Module Loading** (`Module/`): Parses NE-format DLLs, MDF (module definition), MSG (messages), MCV (config values). Each module gets a full 4GB virtual address space.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/CLAUDE.md` line 65 — `3. **Memory** (`Memory/`): `ProtectedModeMemoryCore` provides per-module 4GB address space. `FarPtr` represents segment:offset pointers. Memory is lazily allocated.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/CLAUDE.md` line 66 — `4. **Host Process** (`HostProcess/`): `MbbsHost` manages module lifecycle, session scheduling, and execution units. `ExecutionUnit` wraps CPU contexts for concurrent module operations.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/CLAUDE.md` line 67 — `5. **Exported Modules** (`HostProcess/ExportedModules/`): C# implementations of MajorBBS/Worldgroup APIs that modules call via imported ordinals. Each mapped to a virtual segment:`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/CLAUDE.md` line 69 — `6. **Sessions** (`Session/`): `SessionBase` subclasses handle different connection types (Telnet, Rlogin, local console). Sessions are assigned channel numbers and manage user I/O buffers.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/CLAUDE.md` line 73 — `- **Btrieve** (`Btrieve/`): Database engine emulation backed by SQLite. `BtrieveFileProcessor` handles CRUD operations with key-based access.`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
