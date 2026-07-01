# DataDecode0049 Repository Preservation

This folder preserves the user-provided `DataDecode0049-WGSERVERRecordAccessStructureCorrelation-PreMirror.zip` package in the repository.

## Source Package

- Source path: `C:\Users\djkao\Downloads\DataDecode0049-WGSERVERRecordAccessStructureCorrelation-PreMirror.zip`
- ZIP SHA-256: `8869C12860A5D645F5EBA5B8F6F51C9389A98E96A3ABA7B8CBB57288CCCE8A2B`
- Preserved file count: 17

## WGSERVER Reference

- Source path: `C:\Worldgroup Server\wgserver.exe`
- Size: 432,128 bytes
- SHA-256: `4DA28D718CF99701AA63C1889770F293BB3EC828AEA5E8BEA500473D2698C73F`
- Policy: reference/analysis evidence only. Do not execute, patch, distribute, or make runtime use of this executable.

## Summary

DataDecode0049 is a static-analysis-only WGSERVER record access / structure correlation pass. It reports:

- 15 NativeRuntime DAT datasets profiled.
- 5,916 executable ASCII strings length 4+.
- 9,574 record-size/count/file-size literal hits.
- 29 string-address cross references.
- 29 record-access candidate windows.
- 15 dataset record descriptor candidate rows.
- 93 canonical term correlation hits.
- 0 field promotions.

Literal matches and xrefs remain candidates only. They are not proof of field semantics without binary-backed corroboration.
