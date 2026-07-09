# Command parser behavior

**Mechanic ID:** `CMD.PARSER`  
**Bucket:** Commands and Client  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Command vocabulary, aliases, parser routing, and output contracts.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 2689
- Representative evidence entries: 60
- Source/path coverage: 17

## Representative Evidence

- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/ExportedModules/Majorbbs/parsin_Tests.cs` line 34 — `var expectedParsedInput = Encoding.ASCII.GetBytes(inputCommand.Replace(' ', '\0')[..expectedInputLength]);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/ExportedModules/Majorbbs/parsin_Tests.cs` line 42 — `Assert.True(expectedParsedInput`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/Majorbbs.cs` line 3171 — `///     Returns the pointer to the next parsed input command from the user`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Session/SessionBase.cs` line 112 — `///     Parsed Command Input`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/TextVariables/ITextVariableService.cs` line 42 — `public ReadOnlySpan<byte> Parse(ReadOnlySpan<byte> input, Dictionary<string, TextVariableValue.TextVariableValueDelegate> sessionValues);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/TextVariables/ITextVariableService.cs` line 50 — `ReadOnlySpan<byte> Parse(ReadOnlySpan<byte> input, Dictionary<string, string> sessionValues);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/TextVariables/TextVariableService.cs` line 137 — `public ReadOnlySpan<byte> Parse(ReadOnlySpan<byte> input, Dictionary<string, string> sessionValues)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/TextVariables/TextVariableService.cs` line 202 — `public ReadOnlySpan<byte> Parse(ReadOnlySpan<byte> input, Dictionary<string, TextVariableValue.TextVariableValueDelegate> sessionValues)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/TextVariables/TextVariableService.cs` line 208 — `return Parse(input, invokedInput);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/DOS/Interrupts/Int21h_Tests.cs` line 40 — `_consoleInput.WriteByte((byte)'a');`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/DOS/Interrupts/Int21h_Tests.cs` line 41 — `_consoleInput.WriteByte((byte)'b');`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/ExportedModules/Galgsbl/btuimp_Tests.cs` line 20 — `testSessions[0].InputBuffer.Write(Encoding.ASCII.GetBytes("Test Input String"));`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 22181 — `Dim tItems As ItemParseResult, sInput As String, x As Integer, bSortAfter As Boolean`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/frmMain.frm` line 22207 — `tItems = ParseGameTextInventory(sInput)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modItemParse.bas` line 157 — `Public Function ParseGameTextInventory(ByVal sInput As String) As ItemParseResult`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMain.bas` line 382 — `nArgCount = ParseCommandLineFiles(Command$, sArgs)`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMain.bas` line 421 — `Private Function ParseCommandLineFiles(ByVal sCommandLine As String, _`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/modMain.bas` line 477 — `ParseCommandLineFiles = nCount`
- `MonsterSim` priority 5 — `MonsterSim/MajorMUD-PHP-Monster-Sim-master/capture_parse.php` line 222 — `<br><input type="submit" name="submit_ignore_unparsed" id="submit_ignore_unparsed" value="Upload and remove unparsed lines.">`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 143 — `318|0|325|MISSION command given to wounded messenger`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 182 — `518|0|495|MISSIONS command given to annora`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 205 — `518|0|495|MISSIONS command`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 218 — `1112|0|1113|PERMISSION command`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 273 — `518|0|495|MISSIONS command given to annora`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/NMR-Quests.txt` line 288 — `518|0|495|missions command`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
