# Flags and enumerations

**Mechanic ID:** `DATA.FLAGS_ENUMS`  
**Bucket:** Data Schema  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Bit flags, enums, masks, and symbolic values across data tables.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 21285
- Representative evidence entries: 60
- Source/path coverage: 17

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/modFieldmaps_vO.bas` line 665 — `unknown13(8) As Integer             '18     2006   739  _energy_update_character sets unknown13(0) to 0 if ENCUM_PCT > 66; flags/bitmask in element 8`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/modFieldmaps_vO.bas` line 669 — `'unknown13(8) flags/bitmask here 0x20 = class stealth, 0x40 = race stealth, 0x60 = race+class stealth`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/CPU/FCOMPP_Tests.cs` line 9 — `private readonly ushort FPU_CODE_MASK = (ushort)(EnumFpuStatusFlags.Code0 | EnumFpuStatusFlags.Code1 |`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/CPU/FCOMP_Tests.cs` line 11 — `private readonly ushort FPU_CODE_MASK = (ushort)(EnumFpuStatusFlags.Code0 | EnumFpuStatusFlags.Code1 |`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/CPU/FCOM_Tests.cs` line 11 — `private readonly ushort FPU_CODE_MASK = (ushort)(EnumFpuStatusFlags.Code0 | EnumFpuStatusFlags.Code1 |`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/DOS/Interrupts/Int7Bh_Tests.cs` line 213 — `flags = (ushort) (EnumKeyAttributeMask.Duplicates | EnumKeyAttributeMask.UseExtendedDataType),`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/DOS/Interrupts/Int7Bh_Tests.cs` line 222 — `flags = (ushort) (EnumKeyAttributeMask.Modifiable | EnumKeyAttributeMask.UseExtendedDataType),`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/DOS/Interrupts/Int7Bh_Tests.cs` line 230 — `flags = (ushort) (EnumKeyAttributeMask.Duplicates | EnumKeyAttributeMask.Modifiable | EnumKeyAttributeMask.UseExtendedDataType),`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/DOS/Interrupts/Int7Bh_Tests.cs` line 238 — `flags = (ushort) EnumKeyAttributeMask.UseExtendedDataType,`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Btrieve/BtrieveFile.cs` line 343 — `var attributes = (EnumKeyAttributeMask) BitConverter.ToUInt16(data, 0x8);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Btrieve/BtrieveFile.cs` line 344 — `if (attributes.HasFlag(EnumKeyAttributeMask.UseExtendedDataType))`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Btrieve/BtrieveFile.cs` line 347 — `dataType = attributes.HasFlag(EnumKeyAttributeMask.OldStyleBinary) ? EnumKeyDataType.OldBinary : EnumKeyDataType.OldAscii;`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Btrieve/BtrieveFile.cs` line 355 — `Segment = attributes.HasFlag(EnumKeyAttributeMask.SegmentedKey),`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Btrieve/BtrieveFile.cs` line 356 — `SegmentOf = attributes.HasFlag(EnumKeyAttributeMask.SegmentedKey) ? currentKeyNumber : (ushort)0,`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMRTask_clsMenuItems.cls` line 82 — `Attribute NewEnum.VB_MemberFlags = "40"`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmRoom.frm` line 5964 — `str = "The attributes field is a bitmask, you can turn certain switches on and off and convert the binary bitmask to a decimal number." & vbCrLf`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmUser.frm` line 8207 — `MsgBox "Observed a value of 78 here that indicated the the character was poisoned.  It caused the character to take damage and not be able to rest.  It could be a bitmask or something.", vbInformation`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmUser.frm` line 9006 — `'Text4 = Userrec.Bitmask1 'And &H80`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmUser.frm` line 9007 — `'Text5 = Userrec.Bitmask2`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmUser.frm` line 9116 — `x = 1:  lblUserUnknowns(x).Caption = "Bitmask1": txtUserUnknowns(x).Text = Userrec.Bitmask1`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmUser.frm` line 9152 — `x = 46:  lblUserUnknowns(x).Caption = "Bitmask2": txtUserUnknowns(x).Text = Userrec.Bitmask2`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmUser.frm` line 9767 — `'Userrec.Bitmask1 = Val(Text4.Text)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/modFieldmaps.bas` line 621 — `Bitmask1 As Byte`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/modFieldmaps.bas` line 622 — `Bitmask2 As Byte`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 41843 — `'Bit flag already 0, so no need to do anything`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
