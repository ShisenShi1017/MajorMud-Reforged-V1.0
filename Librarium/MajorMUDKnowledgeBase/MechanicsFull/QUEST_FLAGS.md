# Quest flags/state

**Mechanic ID:** `QUEST.FLAGS`  
**Bucket:** Quests and Scripts  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Persistent quest state, flags, and per-character progress fields.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.

## Evidence Coverage
- Corpus matches: 6864
- Representative evidence entries: 36
- Source/path coverage: 7

## Representative Evidence

- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas` line 3650 — `GetAbilityName = "QuestFlag" & nNum`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas` line 3675 — `GetAbilityName = "QuestFlag" & nNum`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas` line 3684 — `GetAbilityName = "QuestFlag" & nNum`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas` line 3971 — `'sAbility = "QuestFlag" & nNum`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas` line 3996 — `'sAbility = "QuestFlag" & nNum`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMMudFunc.bas` line 4005 — `'sAbility = "QuestFlag" & nNum`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 36 — `9046|0|0|flag checks and class rewards`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 83 — `2688|0|0|(this is just the last part) dao lords death spell casts another spell which executes this block.  it has the flag checks, exp gain, and giving the item.`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 91 — `396|0|397|flag check`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 94 — `399|0|400|flag check`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 99 — `396|0|4027|flag check`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 105 — `4038|0|4033|flag check`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 110 — `4041|0|4037|flag check`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 119 — `396|0|9449|flag check`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 121 — `9446|0|9449|flag check`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 124 — `9451|0|9579|flag check`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 130 — `9290|0|9455|flag check`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 133 — `9292|0|9291|flag check`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/CPU/AND_Tests.cs` line 15 — `public void AND_AL_IMM8(byte alValue, byte valueToAnd, byte expectedResult, bool zeroFlagSet, bool signFlagSet)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/CPU/AND_Tests.cs` line 31 — `//Verify Flags`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/CPU/AND_Tests.cs` line 32 — `Assert.Equal(zeroFlagSet, mbbsEmuCpuRegisters.ZeroFlag);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/CPU/AND_Tests.cs` line 33 — `Assert.Equal(signFlagSet, mbbsEmuCpuRegisters.SignFlag);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/CPU/AND_Tests.cs` line 34 — `Assert.False(mbbsEmuCpuRegisters.OverflowFlag);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/CPU/AND_Tests.cs` line 35 — `Assert.False(mbbsEmuCpuRegisters.CarryFlag);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/CPU/AND_Tests.cs` line 42 — `public void AND_AX_IMM16(ushort axValue, ushort valueToAnd, ushort expectedResult, bool zeroFlagSet, bool signFlagSet)`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
