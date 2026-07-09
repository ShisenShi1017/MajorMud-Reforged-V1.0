# Binary data formats

**Mechanic ID:** `DATA.BINARY_FORMATS`  
**Bucket:** Data Schema  
**Status:** probable_multi_source  
**Runtime:** not_changed_by_phase2_population

## Description
MajorMUD binary table/database structures and parsers.

## Sources Present
- NightmareRedux: Primary current reverse-engineered implementation; preferred over historical Monster Sim on direct conflicts unless contradicted by original runtime evidence.
- MBBSEmu: Independent emulator/runtime reference; useful for corroboration and runtime architecture behavior.
- MMUDExplorer: Data-structure and field interpretation reference; useful for parsing, enums, bit fields, and editor meanings.
- MonsterSim: Historical simulator and regression reference; useful for combat/spell-energy checks but lower priority when it conflicts with newer Nightmare Redux.

## Evidence Coverage
- Corpus matches: 22587
- Representative evidence entries: 60
- Source/path coverage: 16

## Representative Evidence

- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 69 — `-NEW: complete overhaul of database exporter.  support for multiple ranges of records, ability to save config files, **import record numbers from existing database**`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/docs/nmr tools.txt` line 52 — `--This function will close all open databases and compile a database update file for your realm. This is nessisary to save all of the changes you have made to your realm. When a realm first starts up it does what is called a 'database update'. What this does is compare all of your wcc*.dat files to the ones in the wccupda2.dat file, updating any records that do not match.`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 1509 — `Private Sub SetRangeFromDB(ByRef tabTable As Recordset, nIndex As Integer, sIndex As String)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 5321 — `chkExportAll(x).Value = ReadINI("Records_" & x, "List_All", sFile, "1")`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 5324 — `sLine = ReadINI("Records_" & x, "List_" & y, sFile)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 5338 — `sLine = ReadINI("Records_" & x, "List_" & y, sFile)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 5418 — `Call WriteINI("Records_" & x, "List_All", "1", sFile)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 5420 — `Call WriteINI("Records_" & x, "List_All", "0", sFile)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 5430 — `Call WriteINI("Records_" & x, "List_" & y, sTemp, sFile)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/frmDatabaseExport.frm` line 5433 — `Call WriteINI("Records_" & x, "List_" & y, "End", sFile)`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/modMain.bas` line 1118 — `MsgBox "Warning: Invalid room database record size returned while verifying dat file version setting.", vbExclamation`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Btrieve/BtrieveFile.cs` line 479 — `logger.Warn($"Database {FileName} contains {RecordCount} records but only read {recordsLoaded}!");`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Btrieve/BtrieveFileProcessor.cs` line 58 — `///     <para/>Currently only fixed length record databases are supported.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Btrieve/BtrieveFileProcessor.cs` line 63 — `///     Whether the database contains variable length records.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/Btrieve/BtrieveFileProcessor.cs` line 349 — `///     Returns the total number of records contained in the database.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu/UI/Setup/SetupView.cs` line 276 — `"Btrieve DAT files are converted to SQLite at startup and each record is a binary struct. This value speeds up reads of records queried by the Btrieve API by Modules by keeping records in memory and not querying SQLite for each read.";`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/scripts/create_user.py` line 16 — `parser.add_argument('--dbfile', help='Database specified by appsettings.json Database.File', default='mbbsemu.db')`
- `MMUDExplorer` priority 4 — `MMUDExplorer/MMUD-Explorer-master/docs/c-users-matthew-snead-github-megamud2-m-sharded-engelbart.md` line 13 — `MegaMUD**. We parse all `rooms.md` files once into an in-memory database, then while drawing a`
- `NightmareRedux` priority 2 — `NightmareRedux/Nightmare-Redux-master/README.md` line 22 — `-FIX: utilizing the "reset user intractable" fields on database record room exports will no longer remove hidden sign items.`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveFileProcessor_Tests.cs` line 35 — `private const string EXPECTED_METADATA_T_SQL = "CREATE TABLE metadata_t(record_length INTEGER NOT NULL, physical_record_length INTEGER NOT NULL, page_length INTEGER NOT NULL, variable_length_records INTEGER NOT NULL, version INTEGER NOT NULL, acs_name STRING, acs BLOB)";`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveFileProcessor_Tests.cs` line 77 — `btrieve.RecordLength.Should().Be(MBBSEmuRecordStruct.RECORD_LENGTH);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveFileProcessor_Tests.cs` line 79 — `btrieve.VariableLengthRecords.Should().BeFalse();`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveFileProcessor_Tests.cs` line 170 — `btrieve.RecordLength.Should().Be(MBBSEmuRecordStruct.RECORD_LENGTH);`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveFileProcessor_Tests.cs` line 172 — `btrieve.VariableLengthRecords.Should().BeFalse();`
- `MBBSEmu` priority 3 — `MBBSEmu/MBBSEmu-master/MBBSEmu.Tests/Btrieve/BtrieveFileProcessor_Tests.cs` line 226 — `new MBBSEmuRecordStruct(btrieve.GetRecord()).Key1.Should().Be(3444);`

## Implementation Mapping
- Runtime formula/update work must cite this Mechanic ID before implementation.
- Simulator tests should be generated from this record after conflicts are resolved.
