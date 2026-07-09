# Monster/NPC field schema

**Mechanic ID:** `NPC.FIELD_SCHEMA`  
**Bucket:** NPC AI  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Monster data layout, flags, stats, attacks, spells, and behavior fields.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 23718
- Representative evidence entries: 60
- Source/path coverage: 10

## Representative Evidence

- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 33410 — `sAlsoHere = "Also Here (" & tLairInfo.nMaxRegen & "): " & sNPC & IIf(sNPC = "", "", ", ") & GetMultiMonsterNames(tLairInfo.sMobList & ",", bHideRecordNumbers)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMap.frm` line 44715 — `sAlsoHere = "Also Here (" & tLairInfo.nMaxRegen & "): " & sNPC & IIf(sNPC = "", "", ", ") & GetMultiMonsterNames(tLairInfo.sMobList & ",", bHideRecordNumbers)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmHelpMonsters.frm` line 76 — `Attribute VB_Creatable = False`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmHelpMonsters.frm` line 131 — `.Move .Left, .Top, lUseWidth - 125, lUseHeight - TITLEBAR_OFFSET - 760`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmHelpMonsters.frm` line 136 — `.Move .Left, .Top, lUseWidth - 125, lUseHeight - TITLEBAR_OFFSET - 760`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmHelpMonsters.frm` line 141 — `.Move .Left, .Top, lUseWidth - 125, lUseHeight - TITLEBAR_OFFSET - 760`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 647 — `'It looks like it only gets flagged as from bad source if it's a monster greet text or a spell textblock`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMME_Export.frm` line 2841 — `tabMonsters.Fields("ScriptValue") = nMobScriptValue`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonster.frm` line 5773 — `Caption         =   "Type + and - in the '#' field to cycle though the abilities.  You can also type the name of the ability in the 'Name' field."`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonster.frm` line 6211 — `Attribute VB_Creatable = False`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonster.frm` line 6219 — `Dim nCurrentRecord As Long`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonster.frm` line 6260 — `If bLoaded Then Call saverecord(nCurrentRecord)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonster.frm` line 6441 — `MsgBox "No Records Matched.", vbInformation`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonster.frm` line 6693 — `.MINHEIGHT = 475 + (TITLEBAR_OFFSET / 10) + 12`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/DOS/Interrupts/Int7Bh_Tests.cs` line 206 — `btrieveFileSpec.flags.Should().Be(0);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/DOS/Interrupts/Int7Bh_Tests.cs` line 213 — `flags = (ushort) (EnumKeyAttributeMask.Duplicates | EnumKeyAttributeMask.UseExtendedDataType),`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/DOS/Interrupts/Int7Bh_Tests.cs` line 222 — `flags = (ushort) (EnumKeyAttributeMask.Modifiable | EnumKeyAttributeMask.UseExtendedDataType),`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/DOS/Interrupts/Int7Bh_Tests.cs` line 230 — `flags = (ushort) (EnumKeyAttributeMask.Duplicates | EnumKeyAttributeMask.Modifiable | EnumKeyAttributeMask.UseExtendedDataType),`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/DOS/Interrupts/Int7Bh_Tests.cs` line 238 — `flags = (ushort) EnumKeyAttributeMask.UseExtendedDataType,`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Btrieve/BtrieveFile.cs` line 275 — `var accelFlags = BitConverter.ToUInt16(Data.AsSpan().Slice(0xA, 2));`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Btrieve/BtrieveFile.cs` line 276 — `if (accelFlags != 0)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Btrieve/BtrieveFile.cs` line 277 — `return (false, $"Valid accel flags, expected 0, got {accelFlags}! {FileName}");`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Btrieve/BtrieveFile.cs` line 344 — `if (attributes.HasFlag(EnumKeyAttributeMask.UseExtendedDataType))`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Btrieve/BtrieveFile.cs` line 347 — `dataType = attributes.HasFlag(EnumKeyAttributeMask.OldStyleBinary) ? EnumKeyDataType.OldBinary : EnumKeyDataType.OldAscii;`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Btrieve/BtrieveFile.cs` line 355 — `Segment = attributes.HasFlag(EnumKeyAttributeMask.SegmentedKey),`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
