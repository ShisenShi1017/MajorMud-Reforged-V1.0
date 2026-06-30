# Original Reference Capture Tool

`Capture-OriginalReferenceData.ps1` captures original/reference files without running them.

It is intended for MajorMUD, Worldgroup, WCC, WBB, WSGSERVE, and related source material that must remain reference-only until Reforged-native `Data/` is reviewed and approved.

## What It Does

- Preserves source archives under `ImportedDatabases/ReferenceCaptures/<CaptureName>/SourceArchives/`.
- Extracts `.zip` archives using built-in .NET support.
- Extracts `.rar` and `.7z` archives when 7-Zip is installed.
- Walks directories and files.
- Computes SHA-256 hashes.
- Produces file inventory CSV.
- Extracts printable strings.
- Produces string inventory CSV and per-file string reports.
- Adds category hints for rooms, items, monsters, shops, spells, classes, races, quests, trainers, messages, ganghouses, raw legacy databases, configuration, and executable runtime files.

## What It Does Not Do

- It does not run original EXEs, DLLs, COM files, or SYS files.
- It does not use keygens.
- It does not bypass licensing.
- It does not write to canonical `Data/`.
- It does not delete imported/reference material.
- It does not claim full decoding of proprietary binary formats.

## Example

From the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File Tools\ReferenceCapture\Capture-OriginalReferenceData.ps1 -CaptureName WorldgroupServer -SourcePath "C:\Worldgroup Server.rar"
```

If 7-Zip is not installed, `.rar` archives are preserved and reported as needing external extraction. You can manually extract the archive into a folder and rerun:

```powershell
powershell -ExecutionPolicy Bypass -File Tools\ReferenceCapture\Capture-OriginalReferenceData.ps1 -CaptureName WorldgroupServerExtracted -SourcePath "C:\MajorMudReferenceLab\WorldgroupServer"
```

## Output

Reports are written to:

```text
Reports/ReferenceCaptures/<CaptureName>/
```

Reference copies/extractions are written to:

```text
ImportedDatabases/ReferenceCaptures/<CaptureName>/
```

These outputs are reference-only evidence for future reviewed import/reconciliation updates.
