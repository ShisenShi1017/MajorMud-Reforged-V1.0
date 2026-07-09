# Buff/debuff stacking

**Mechanic ID:** `MAGIC.BUFF_STACKING`  
**Bucket:** Magic  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Whether spell effects stack, refresh, overwrite, or conflict.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.

## Evidence Coverage
- Corpus matches: 2521
- Representative evidence entries: 36
- Source/path coverage: 11

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmBank.frm` line 235 — `nStatus = BTRCALL(BGETFIRST, BankPosBlock, BankDatabuf, Len(BankDatabuf), ByVal BankKeyBuffer, KEY_BUF_LEN, 0)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmBank.frm` line 248 — `nStatus = BTRCALL(BGETNEXT, BankPosBlock, BankDatabuf, Len(BankDatabuf), ByVal BankKeyBuffer, KEY_BUF_LEN, 0)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmBank.frm` line 265 — `nStatus = BTRCALL(BGETNEXT, BankPosBlock, BankDatabuf, Len(BankDatabuf), ByVal BankKeyBuffer, KEY_BUF_LEN, 0)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmBank.frm` line 274 — `nStatus = BTRCALL(BGETLAST, BankPosBlock, BankDatabuf, Len(BankDatabuf), ByVal BankKeyBuffer, KEY_BUF_LEN, 0)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmBank.frm` line 299 — `nStatus = BTRCALL(BGETFIRST, BankPosBlock, BankDatabuf, Len(BankDatabuf), ByVal BankKeyBuffer, KEY_BUF_LEN, 0)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmBank.frm` line 326 — `nStatus = BTRCALL(BGETFIRST, BankPosBlock, BankDatabuf, Len(BankDatabuf), ByVal BankKeyBuffer, KEY_BUF_LEN, 0)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmBank.frm` line 403 — `nStatus = BTRCALL(BGETFIRST, BankPosBlock, BankDatabuf, Len(BankDatabuf), ByVal BankKeyBuffer, KEY_BUF_LEN, 0)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmBank.frm` line 414 — `nStatus = BTRCALL(BGETNEXT, BankPosBlock, BankDatabuf, Len(BankDatabuf), ByVal BankKeyBuffer, KEY_BUF_LEN, 0)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmBank.frm` line 425 — `nStatus = BTRCALL(BGETPREVIOUS, BankPosBlock, BankDatabuf, Len(BankDatabuf), ByVal BankKeyBuffer, KEY_BUF_LEN, 0)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmBank.frm` line 436 — `nStatus = BTRCALL(BGETLAST, BankPosBlock, BankDatabuf, Len(BankDatabuf), ByVal BankKeyBuffer, KEY_BUF_LEN, 0)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmBank.frm` line 458 — `nStatus = BTRCALL(BDELETE, BankPosBlock, BankDatabuf, Len(BankDatabuf), ByVal BankKeyBuffer, KEY_BUF_LEN, 0)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmBank.frm` line 488 — `nStatus = BTRCALL(BINSERT, BankPosBlock, BankDatabuf, Len(BankDatabuf), ByVal BankKeyBuffer, KEY_BUF_LEN, 0)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/CLAUDE.md` line 70 — `- `FpuStack` — `double[8]` array for x87 FPU, indexed via StatusWord bits 11-13`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/CLAUDE.md` line 80 — `The FPU registers struct maintains a circular stack via `GetStackTop()`/`PushStackTop()`/`PopStackTop()` with 3-bit stack pointer in StatusWord.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/CPU/CLAUDE.md` line 72 — `- **Stack top** is bits 11-13 of `StatusWord`. `GetStackTop()` extracts it, `SetStackTop()` writes it.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/CPU/RegistersStructs.cs` line 23 — `public byte GetStackTop() => (byte) ((StatusWord >> 11) & 0x7);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/Enums/EnumUserStatus.cs` line 1 — `namespace MBBSEmu.HostProcess.Enums`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/ExportedModuleBase.cs` line 720 — `private protected ReadOnlySpan<byte> ProcessTextVariables(ReadOnlySpan<byte> outputBuffer)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/ExportedModuleBase.cs` line 899 — `//Process Escape Characters for IF-ANSI and write to output buffer`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/Galgsbl.cs` line 789 — `if (channel.DataToProcess && channel.GetStatus() == EnumUserStatus.CR_TERMINATED_STRING_AVAILABLE)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/Majorbbs.cs` line 321 — `//Bail if it's max value, not processing any input or status`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/Majorbbs.cs` line 372 — `///     Handles processing of Channel Input by setting the INPUT value of the dta in the channel InputCommand buffer`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/Majorbbs.cs` line 2097 — `var outputBufferProcessed = FormatOutput(outputBuffer);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/Majorbbs.cs` line 2100 — `ChannelDictionary[userChannel].SendToClient(outputBufferProcessed.ToArray());`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/CLAUDE.md` line 51 — `code. Hand-editing the control/layout block is risky; prefer editing the procedure code at`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
