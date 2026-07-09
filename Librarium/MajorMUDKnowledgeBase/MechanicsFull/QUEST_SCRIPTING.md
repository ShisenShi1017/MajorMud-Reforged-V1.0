# Quest/script hooks

**Mechanic ID:** `QUEST.SCRIPTING`  
**Bucket:** Quests and Scripts  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Quest flags, script triggers, conditions, rewards, and progression hooks.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.

## Evidence Coverage
- Corpus matches: 1329
- Representative evidence entries: 36
- Source/path coverage: 3

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 36 — `9046|0|0|flag checks and class rewards`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 507 — `9474|0|0|class/flag checks, then rewards`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 508 — `9474|1|0|class/flag checks, then rewards`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 709 — `9489|0|0|class/flag checks and rewards`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 83 — `2688|0|0|(this is just the last part) dao lords death spell casts another spell which executes this block.  it has the flag checks, exp gain, and giving the item.`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 91 — `396|0|397|flag check`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 94 — `399|0|400|flag check`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 99 — `396|0|4027|flag check`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 105 — `4038|0|4033|flag check`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 110 — `4041|0|4037|flag check`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 119 — `396|0|9449|flag check`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 121 — `9446|0|9449|flag check`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/CPU/AND_Tests.cs` line 15 — `public void AND_AL_IMM8(byte alValue, byte valueToAnd, byte expectedResult, bool zeroFlagSet, bool signFlagSet)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/CPU/AND_Tests.cs` line 25 — `//Process Instruction`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/CPU/AND_Tests.cs` line 31 — `//Verify Flags`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/CPU/AND_Tests.cs` line 32 — `Assert.Equal(zeroFlagSet, mbbsEmuCpuRegisters.ZeroFlag);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/CPU/AND_Tests.cs` line 33 — `Assert.Equal(signFlagSet, mbbsEmuCpuRegisters.SignFlag);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/CPU/AND_Tests.cs` line 34 — `Assert.False(mbbsEmuCpuRegisters.OverflowFlag);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/CPU/AND_Tests.cs` line 35 — `Assert.False(mbbsEmuCpuRegisters.CarryFlag);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/CPU/AND_Tests.cs` line 42 — `public void AND_AX_IMM16(ushort axValue, ushort valueToAnd, ushort expectedResult, bool zeroFlagSet, bool signFlagSet)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/CPU/AND_Tests.cs` line 52 — `//Process Instruction`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/CPU/AND_Tests.cs` line 58 — `//Verify Flags`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/CPU/AND_Tests.cs` line 59 — `Assert.Equal(zeroFlagSet, mbbsEmuCpuRegisters.ZeroFlag);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/CPU/AND_Tests.cs` line 60 — `Assert.Equal(signFlagSet, mbbsEmuCpuRegisters.SignFlag);`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modListViewExt.bas` line 13 — `'   2) Sticky grouped sorting by action flag (CARRIED, STASH, MANUAL, BUY/SELL, DROP, HIDE, PICKUP/USE)`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
