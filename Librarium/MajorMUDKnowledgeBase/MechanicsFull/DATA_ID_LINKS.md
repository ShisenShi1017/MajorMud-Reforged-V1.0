# ID/link relationships

**Mechanic ID:** `DATA.ID_LINKS`  
**Bucket:** Data Schema  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Cross-table references between rooms, NPCs, items, spells, shops, quests, and effects.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 2493
- Representative evidence entries: 42
- Source/path coverage: 23

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 181 — `Public Property Let nBetweenRoundResistDmgMR(ByVal Index As Integer, ByVal iData As Integer)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 182 — `m_nBetweenRoundResistDmgMR(Index) = iData`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 189 — `Public Property Let nBetweenRoundResistType(ByVal Index As Integer, ByVal iData As Integer)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/clsMonsterAttackSim.cls` line 190 — `m_nBetweenRoundResistType(Index) = iData`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonsterIndex.frm` line 110 — `.MINWIDTH = 500 + (TITLEBAR_OFFSET / 10)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonsterIndexChanger.frm` line 874 — `tabWorkingTable.Index = "idxRooms"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonsterIndexChanger.frm` line 904 — `MsgBox "Group Invalid on Monster #" & tabMonsters.Fields("Number") & " (" & ClipNull(tabMonsters.Fields("Name")) & ")", vbExclamation`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonsterIndexChanger.frm` line 924 — `MsgBox "Mon Type Invalid on Room #" & tabRooms.Fields("Map Number") & "/" & tabRooms.Fields("Room Number"), vbExclamation`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmRecordChange.frm` line 754 — `tabWorkingTable.Index = "idxRooms"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmRecordChange.frm` line 770 — `tabWorkingTable.Index = "idxTextblocks"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/modFieldmaps_vO.bas` line 644 — `'Bit 15 (mask 0x8000): reference to having a character but not paid on the bbs`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/readme.rtf` line 41 — `\snext11 \ssemihidden \sunhideused \sqformat Normal Table;}{\*\cs15 \additive \rtlch\fcs1 \af0 \ltrch\fcs0 \ul\cf17 \sbasedon10 \sunhideused \styrsid1602527 Hyperlink;}}{\*\listtable{\list\listtemplateid-1560145574\listhybrid{\listlevel\levelnfc23`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/DOS/Interrupts/Int7Bh_Tests.cs` line 811 — `public void GetDirectRecordBadIndex()`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/ExportedModules/Majorbbs/strchr_Tests.cs` line 40 — `Assert.Equal(strPointer.Offset + a.IndexOf(toFind), mbbsEmuCpuRegisters.AX);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/CPU/CLAUDE.md` line 126 — `**Why a giant switch and not a dictionary/delegate table:** The large `switch` on `Mnemonic` is intentional. When the .NET JIT compiles this to native code, it emits an optimized jump table — a single indexed jump instruction rather than a chain of comparisons or dictionary lookups. This makes dispatch O(1) with minimal overhead (one bounds check + indirect jump), which matters critically here sin`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Disassembler/Artifacts/NonResidentName.cs` line 9 — `public ushort IndexIntoEntryTable { get; set; }`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Disassembler/Artifacts/ResidentName.cs` line 9 — `public ushort IndexIntoEntryTable { get; set; }`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Disassembler/NEFile.cs` line 157 — `residentName.IndexIntoEntryTable = BitConverter.ToUInt16(FileContent, WindowsHeader.ResidentNameTableOffset + i);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Disassembler/NEFile.cs` line 242 — `NonResidentNameTable.Add(new NonResidentName() { Name = name, IndexIntoEntryTable = indexIntoEntryTable });`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/Structs/UserAccount.cs` line 426 — `return Encoding.ASCII.GetString(useridSpan.Slice(0, Array.IndexOf(userid, (byte)0x0)));`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Memory/ProtectedModeMemoryCore.cs` line 244 — `public override FarPtr GetBigMemoryBlock(FarPtr block, ushort index) => _bigMemoryBlocks[block.Offset][index];`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Module/MBBSModule.cs` line 252 — `var initEntryPoint = dll.File.EntryTable.First(x => x.Ordinal == initNonResidentName.IndexIntoEntryTable);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Module/MBBSModule.cs` line 259 — `var initEntryPoint = dll.File.EntryTable.First(x => x.Ordinal == initResidentName.IndexIntoEntryTable);`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 243 — `Public Property Let nBetweenRoundResistDmgMR(ByVal Index As Integer, ByVal iData As Integer)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/clsMonsterAttackSim.cls` line 244 — `m_nBetweenRoundResistDmgMR(Index) = iData`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
