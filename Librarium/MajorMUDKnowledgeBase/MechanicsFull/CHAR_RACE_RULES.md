# Race modifiers and restrictions

**Mechanic ID:** `CHAR.RACE_RULES`  
**Bucket:** Character Progression  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
Race-specific stat modifiers, restrictions, and abilities.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.

## Evidence Coverage
- Corpus matches: 497
- Representative evidence entries: 34
- Source/path coverage: 19

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 84 — `-BUGFIX: fixed item race restriction fieldmap locations`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 280 — `-FIX: class/race editors were reloading the record lists on close`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/changelog.txt` line 97 — `-BUGFIX: fixed item race restriction fieldmap locations`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/changelog.txt` line 293 — `-FIX: class/race editors were reloading the record lists on close`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseDeleter.frm` line 1473 — `CalcTotalRecords = CalcTotalRecords + Val(txtRacesTo.Text) - Val(txtRacesFrom.Text) + 1`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 1022 — `Dim tabRaces As Recordset`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 1227 — `MsgBox "Error getting first race record: " & BtrieveErrorCode(nStatus)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 1416 — `If Not tabRaces Is Nothing Then nTotalRec = nTotalRec + tabRaces.RecordCount`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 2022 — `Set tabRaces = DB.OpenRecordset("Races")`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 2898 — `tabItems.Fields("Race " & x) = Itemrec.Race(x)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 3988 — `MsgBox "Races: Could not get first record, Error: " & BtrieveErrorCode(nStatus)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 4016 — `ts.Write (Racerec.HPBonus & vbTab)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Assets/MBBSEMU.MSG` line 129 — `processing will suspend itself until system performance`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Btrieve/BtrieveFile.cs` line 275 — `var accelFlags = BitConverter.ToUInt16(Data.AsSpan().Slice(0xA, 2));`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Btrieve/BtrieveFile.cs` line 276 — `if (accelFlags != 0)`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Btrieve/BtrieveFile.cs` line 277 — `return (false, $"Valid accel flags, expected 0, got {accelFlags}! {FileName}");`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Extensions/ReadOnlySpanExtensions.cs` line 40 — `///     Creates a human-readable hex string from a ReadOnlySpan of bytes`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/ExportedModules/Galgsbl.cs` line 96 — `// and trigger a race condition where the timer callback fires even though the test has`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/HostProcess/MbbsHost.cs` line 195 — `_cleanupTimer = new Timer(_ => _performCleanup = true, null, NowUntil(_cleanupTime + _cleanupGracePeriod), TimeSpan.FromDays(1));`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Logging/MessageLogger.cs` line 52 — `target.Write(logLevel, exception.StackTrace);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Module/MBBSModule.cs` line 339 — `//Set AX == 1, we don't return an exit code so it can be gracefully handled by the host process`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/UI/Main/MainView.cs` line 282 — `LogLevel.Trace => "TRACE",`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/CLAUDE.md` line 66 — ``tabShops`, `tabRaces`, `tabClasses`, `tabLairs`, etc. Combat/damage result arrays`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 3 — `MMUD Explorer is a database viewer for the game MajorMUD(r) created by syntax53. It has a unique comparing feature which allows you to easily compare weapons, armour, and spells. It also has a graphical room explorer in which you can 'walk' around the realm. Other features include an inventory calculator, exp calculator, explorers for monsters/shops/weapons/armour/spells/items/races/classes, savin`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/README.md` line 120 — `-UP: Level range on character builder now dynamic based on race stats`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
