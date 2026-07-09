# Saving throws and immunities

**Mechanic ID:** `STATUS.SAVES`  
**Bucket:** Status Effects  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Save checks, immunity fields, and status prevention.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 950
- Representative evidence entries: 60
- Source/path coverage: 11

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 47 — `-UP: Changed drop down spell editor's "Type of Resist" to what I believe they actually mean`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 52 — `-UP: Added spell resist type (if the spell is completely resistible)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 69 — `-NEW: complete overhaul of database exporter.  support for multiple ranges of records, ability to save config files, **import record numbers from existing database**`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 133 — `-UP: clicking save will now update the text on the row in the record list`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 164 — `-UP: added boxes to show spell damage vs. X MR for damage spells`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 258 — `-FIX: had to save and re-click to see markup change on shops`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 314 — `-NEW: "Charm Resistance" field for monsters added (thx Tsunami)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 30 — `Private m_nAtkResist(0 To 4) As Integer`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 31 — `Private m_nAtkMRdmgResist(0 To 4) As Integer`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 36 — `Private m_nStatAtkDmgResisted(0 To 4) As Currency`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 37 — `Private m_nStatAtkAttemptDodgedOrResisted(0 To 4) As Currency`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 40 — `Private m_nStatHitSpell_AtkDmgResisted(0 To 4) As Currency`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/CPU/CPUCore.cs` line 3416 — `///     Set Status Flag Values from Flags Saved in AH`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/CPU/CPUCore.cs` line 3667 — `Memory.SetWord(destinationSegment, offset, (ushort)Convert.ToInt16(valueToSave));`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/CPU/CPUCore.cs` line 3679 — `Memory.SetArray(destinationSegment, offset, BitConverter.GetBytes(valueToSave));`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/CPU/CPUCore.cs` line 3729 — `Memory.SetArray(Registers.GetValue(_currentInstruction.MemorySegment), GetOperandOffset(_currentInstruction.Op0Kind), BitConverter.GetBytes((float)valueToSave));`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/CPU/CPUCore.cs` line 3735 — `Memory.SetArray(Registers.GetValue(_currentInstruction.MemorySegment), GetOperandOffset(_currentInstruction.Op0Kind), BitConverter.GetBytes(valueToSave));`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/CLAUDE.md` line 94 — `- Btrieve helpers: `BtrieveSaveProcessor()`, `BtrieveGetProcessor()`, `BtrieveDeleteProcessor()`, `BtrieveSetupGlobalPointer()``
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/ExportedModuleBase.cs` line 1141 — `///     Saves a BtrieveFileProcessor for the given pointer to the Global Cache`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/ExportedModuleBase.cs` line 1145 — `private protected void BtrieveSaveProcessor(FarPtr btrievePointer, BtrieveFileProcessor btrieveFileProcessor) =>`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/ExportedModuleBase.cs` line 1171 — `///     Generates a Unique Key to be used for saving a BtrieveFileProcessor mapped to its pointer`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/ExportedModuleBase.cs` line 1218 — `//Save a local reference to the shared Processor`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/ExportedModuleBase.cs` line 1219 — `BtrieveSaveProcessor(btrievePointer, _globalCache.Get<BtrieveFileProcessor>($"{variableName}-PROCESSOR"));`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/Majorbbs.cs` line 1603 — `_logger.Debug($"({Module.ModuleIdentifier}) Retrieved Module Name \"{moduleName}\" and saved it at host memory offset {variablePointer}");`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 87 — `-FIX: Spells with ability 144-NonMagicalSpell (hidden in MME due to prevalence) will no longer show as Damage(-MR)`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
