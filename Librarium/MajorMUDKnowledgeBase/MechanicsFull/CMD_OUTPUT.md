# Output/message formatting

**Mechanic ID:** `CMD.OUTPUT`  
**Bucket:** Commands and Client  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Classic-style text responses, combat messages, and UI-facing strings.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 2689
- Representative evidence entries: 38
- Source/path coverage: 18

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmItem.frm` line 4078 — `'txtMissMsgDisplay.Text = GetMessages(Itemrec.MissMsg, 1)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmItem.frm` line 4379 — `txtMissMsgDisplay.Text = GetMessages(Val(txtMissMsg.Text), 1)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonster.frm` line 7175 — `'txtAttackDodgeMsgDisplay(x).Text = GetMessages(Monsterrec.AttackDodgeMsg(x), 3)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonster.frm` line 7176 — `'txtAttackMissMsgDisplay(x).Text = GetMessages(Monsterrec.AttackMissMsg(x), 2)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonster.frm` line 7532 — `txtAttackDodgeMsgDisplay(Index).Text = GetMessages(Val(txtAttackDodgeMsg(Index).Text), 3)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmMonster.frm` line 7606 — `txtAttackMissMsgDisplay(Index).Text = GetMessages(Val(txtAttackMissMsg(Index).Text), 2)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmSpell.frm` line 2595 — `'txtCastADisplay.Text = GetMessages(Spellrec.CastMsgA, 1)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmSpell.frm` line 2596 — `'txtCastBDisplay.Text = GetMessages(Spellrec.CastMsgB, 2)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmSpell.frm` line 2718 — `txtCastADisplay.Text = GetMessages(Val(txtCastMsgA.Text), 1)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmSpell.frm` line 2727 — `txtCastBDisplay.Text = GetMessages(Val(txtCastMsgB.Text), 2)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseDeleter.frm` line 1459 — `CalcTotalRecords = CalcTotalRecords + Val(txtMessagesTo.Text) - Val(txtMessagesFrom.Text) + 1`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseImport.frm` line 3085 — `CalcTotalRecords = CalcTotalRecords + Val(txtMessagesTo.Text) - Val(txtMessagesFrom.Text) + 1`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/CLAUDE.md` line 83 — ``FormatOutput()` chains three processors for text sent to clients:`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/Doscalls.cs` line 30 — `internal Doscalls(IClock clock, IMessageLogger logger, AppSettingsManager configuration, IFileUtility fileUtility, IGlobalCache globalCache, MbbsModule module, PointerDictionary<SessionBase> channelDictionary, ITextVariableService textVariableService) : base(`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/ExportedModuleBase.cs` line 115 — `private protected ExportedModuleBase(IClock clock, IMessageLogger logger, AppSettingsManager configuration, IFileUtility fileUtility, IGlobalCache globalCache, MbbsModule module, PointerDictionary<SessionBase> channelDictionary, ITextVariableService textVariableService)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/ExportedModuleBase.cs` line 720 — `private protected ReadOnlySpan<byte> ProcessTextVariables(ReadOnlySpan<byte> outputBuffer)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/ExportedModuleBase.cs` line 761 — `return _textVariableService.Parse(outputBuffer, txtvarDictionary);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/ExportedModuleBase.cs` line 1136 — `output.Write(ProcessTextVariables(FormatNewLineCarriageReturn(ProcessIfANSI(outputBytes))));`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/Galgsbl.cs` line 45 — `public Galgsbl(IClock clock, IMessageLogger logger, AppSettingsManager configuration, IFileUtility fileUtility, IGlobalCache globalCache, MbbsModule module, PointerDictionary<SessionBase> channelDictionary, ITextVariableService textVariableService) : base(`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/Galme.cs` line 26 — `internal Galme(IClock clock, IMessageLogger logger, AppSettingsManager configuration, IFileUtility fileUtility, IGlobalCache globalCache, MbbsModule module, PointerDictionary<SessionBase> channelDictionary, ITextVariableService textVariableService) : base(`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/Galmsg.cs` line 23 — `internal Galmsg(IClock clock, IMessageLogger logger, AppSettingsManager configuration, IFileUtility fileUtility, IGlobalCache globalCache, MbbsModule module, PointerDictionary<SessionBase> channelDictionary, ITextVariableService textVariableService) : base(`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/Majorbbs.cs` line 132 — `public Majorbbs(IClock clock, IMessageLogger logger, AppSettingsManager configuration, IFileUtility fileUtility, IGlobalCache globalCache, MbbsModule module, PointerDictionary<SessionBase> channelDictionary, IAccountKeyRepository accountKeyRepository, IAccountRepository accountRepository, ITextVariableService textVariableService) : base(`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/Majorbbs.cs` line 4021 — `var output = McvPointerDictionary[_currentMcvFile.Offset].GetString(messageNumber);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/Majorbbs.cs` line 4031 — `_logger.Debug($"({Module.ModuleIdentifier}) Added {output.Length} bytes to the buffer (Message #: {messageNumber})");`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 389 — `-UP: if a spell executes a textblock and that textblock only casts spells, the spells will be displayed in the detail window`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
