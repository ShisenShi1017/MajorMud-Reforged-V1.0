# Imported Reference Archive Manifest

Status: CodeUpdate0140 local-only archive created for repository cleanup.

Archive:

- `Archive/ImportedDatabasesAndEvidence.LocalOnly.rar`

Archived loose roots:

- `ImportedDatabases/`
- `Evidence/`

Committed placeholders:

- `ImportedDatabases/README.md`
- `Evidence/README.md`

Policy:

- Librarium is the committed gameplay authority.
- `/Data` is generated runtime output and is not canonical source.
- Imported databases and raw evidence are retained locally for future decode, provenance, or source-retirement audits.
- Raw decoded/imported evidence is no longer required in Git when promoted Librarium records plus current validation reports preserve the active proof boundary.

Promoted before archival:

- Gang-house Librarium records now carry the generated 57-location authority.
- Room environment anchors for `room.map1.288` and `room.map1.2306` are retained in Librarium as imported WCC room-record anchors.

Verification:

- RAR integrity test completed successfully with WinRAR.
- `dotnet build MajorMud.Reforged.sln --configuration Release --no-restore` passed with 0 warnings and 0 errors after archival cleanup.
- `python Tools\validator.py` passed 15/15 current-health checks after archival cleanup.
