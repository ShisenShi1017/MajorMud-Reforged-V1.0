# Account/character persistence contracts

**Mechanic ID:** `RUNTIME.PERSISTENCE`  
**Bucket:** Runtime Support  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Save/load data, slots, character state, and world isolation.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 4903
- Representative evidence entries: 52
- Source/path coverage: 14

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 69 — `-NEW: complete overhaul of database exporter.  support for multiple ranges of records, ability to save config files, **import record numbers from existing database**`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 133 — `-UP: clicking save will now update the text on the row in the record list`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 163 — `-UP: spell calculations will now take energy into account`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 241 — `-NEW: shops now show bank account/cost info for gangshops`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 258 — `-FIX: had to save and re-click to see markup change on shops`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 280 — `-FIX: class/race editors were reloading the record lists on close`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 358 — `-UP: optimized some loading code in users/monsters/spells/shops/items`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 362 — `-UP: fixed some tab orders and made search/goto fields have focus on load`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/cRegistry.cls` line 4 — `Persistable = 0  'NotPersistable`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/changelog.txt` line 74 — `records, ability to save config files, **import record numbers from existing`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/changelog.txt` line 146 — `-UP: clicking save will now update the text on the row in the record list`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/changelog.txt` line 176 — `-UP: spell calculations will now take energy into account`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Module/MsgFile_Tests.cs` line 85 — `File.WriteAllBytes(fileName, Load("MBBSEMU.MSG").ToArray());`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Module/MsgFile_Tests.cs` line 89 — `File.ReadAllBytes(fileName).Should().BeEquivalentTo(Load("MBBSEMU.MSG").ToArray());`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Module/MsgFile_Tests.cs` line 98 — `File.WriteAllBytes(fileName, Load("MBBSEMU.MSG").ToArray());`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/CLAUDE.md` line 3 — `This directory contains the C# implementations of the MajorBBS/Worldgroup SDK libraries. When a loaded module's x86 code executes a `CALL FAR` to a virtual segment (>= 0xFF00), the CPU fires `_invokeExternalFunctionDelegate`, which routes to the appropriate exported module's `Invoke(ordinal)` method. These classes are the bridge between the emulated 16-bit world and modern C#.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/CLAUDE.md` line 14 — `| `Doscalls.cs` | ~175 | OS/2-style DOS calls: DosAllocSeg, DosLoadModule, DosGetProcAddr, DosGetModHandle |`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/CLAUDE.md` line 75 — `**Important**: Pointer parameters consume 2 ordinal slots (offset + segment). So if param 0 is a pointer, the next scalar is at ordinal 2, not 1.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/CLAUDE.md` line 94 — `- Btrieve helpers: `BtrieveSaveProcessor()`, `BtrieveGetProcessor()`, `BtrieveDeleteProcessor()`, `BtrieveSetupGlobalPointer()``
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/CLAUDE.md` line 137 — `**Properties** (Phase 1) return a 4-byte `ReadOnlySpan<byte>` containing a `FarPtr` that points to the variable's location in module memory. These are used by the NE relocation process — the loader patches the module's code with the returned address.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/CLAUDE.md` line 194 — `- `USRACC` / `USAPTR` (account records), `EXTUSR` / `EXTPTR``
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/ExportedModuleBase.cs` line 1141 — `///     Saves a BtrieveFileProcessor for the given pointer to the Global Cache`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/ExportedModuleBase.cs` line 1145 — `private protected void BtrieveSaveProcessor(FarPtr btrievePointer, BtrieveFileProcessor btrieveFileProcessor) =>`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/ExportedModuleBase.cs` line 1218 — `//Save a local reference to the shared Processor`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/CLAUDE.md` line 65 — ``Recordset` objects opened once at load: `tabItems`, `tabMonsters`, `tabSpells`, `tabRooms`,`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
