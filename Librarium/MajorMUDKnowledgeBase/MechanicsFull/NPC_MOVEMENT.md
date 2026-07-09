# NPC movement behavior

**Mechanic ID:** `NPC.MOVEMENT`  
**Bucket:** NPC AI  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Roaming, following, fleeing, and room movement behavior.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 375
- Representative evidence entries: 39
- Source/path coverage: 19

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_modDeclares.bas` line 324 — `Public Declare Sub CopyMem Lib "kernel32" Alias "RtlMoveMemory" (pDest As Any, pSource As Any, ByVal ByteLen As Long)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_modDeclares.bas` line 545 — `DefWindowProc hWnd, WM_SYSCOMMAND, SC_MOVE, 0`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 22 — `-FIX: utilizing the "reset user intractable" fields on database record room exports will no longer remove hidden sign items.`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 124 — `-NEW: option to add/remove EDITED flag on users`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 162 — `-UP: moved around spell editor fields again`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 601 — `-NEW: Added the ability to remove all item level restrictions`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/cRegistry.cls` line 52 — `Private Declare Sub CopyMemory Lib "kernel32" Alias "RtlMoveMemory" (dest As Any, Source As Any, ByVal numBytes As Long)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/changelog.txt` line 20 — `-FIX: utilizing the "reset user intractable" fields on database record room exports will no longer remove hidden sign items.`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/changelog.txt` line 137 — `-NEW: option to add/remove EDITED flag on users`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/changelog.txt` line 175 — `-UP: moved around spell editor fields again`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/nmr tools.txt` line 9 — `Export: Allows you to export part or all of any database(s) in the game. This function is highly useful when you need to move specific databases (or ranges thereof) between realms and serves as an excellent backup function. It is also a great tool for upgrading versions, or even downgrading as far as version 1.11h. Lets say you have version 1.11i and would like to upgrade to version 1.11m, but hav`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/nmr tools.txt` line 17 — `---Allows you to create a limited item list scanning in some or all of the following databases: Users, rooms and shops. You may also save the list for future reference. Very useful for keeping track of your limiteds.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/ExportedModules/Majorbbs/movmem_Tests.cs` line 50 — `public void movmem_samepointer_Test(ushort moveLength, ushort bufferShift, byte[] source, byte[] expected)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/CPU/CLAUDE.md` line 175 — `This three-tier pattern (dispatch → size select → compute+flags+write) is consistent across ADD, SUB, AND, OR, XOR, INC, DEC, NEG, CMP, TEST, SHL, SHR, SAR, RCR, RCL, ROR, SHLD, etc. When adding a new instruction, follow this same pattern.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/CPU/CPUCore.cs` line 4469 — `///     Move with Sign-Extend to 16bit destination`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/CPU/CPUCore.cs` line 4479 — `///     Move with Sign-Extend to 32bit destination`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/CPU/CPUCore.cs` line 4523 — `///     Move with Zero-Extend to 16bit destination`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/CPU/CPUCore.cs` line 4533 — `///     Move with Zero-Extend to 32bit destination`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Disassembler/NEFile.cs` line 193 — `//0xFF is moveable (6 bytes), anything else is fixed as it becomes the segment number`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Extensions/ByteExtensions.cs` line 49 — `public static byte RemoveFlag(this byte b, byte bitMask) => (byte)(b & ~bitMask);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/ExportedModuleBase.cs` line 37 — `/// <summary>True to indicate there is more input following what was parsed</summary>`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/Majorbbs.cs` line 2454 — `McvPointerDictionary.Remove(filePointer.Offset);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/Majorbbs.cs` line 3385 — `FilePointerDictionary.Remove(fileStruct.curp.Offset);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/Majorbbs.cs` line 4358 — `var bytesToMove = GetParameter(4);`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 109 — `-FIX: Items not removed from saved list when equipped from item manager (setting)`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
