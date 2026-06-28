# CodeUpdate-0007 Apply Report

## Purpose

Rebuilt Core Update 0007 from scratch to include the no-SDK runtime rule and the extracted-folder execution rule.

## Safety

- Does not include `Saves/`.
- Does not include `ImportedDatabases/`.
- Does not require the .NET SDK to apply or run.
- Applies by file copy from this update folder into the main project folder.

## Validation performed in packaging environment

- Verified update ZIP exists.
- Verified package includes `Apply-CodeUpdate-0007-Core-Contracts-Foundation.bat`.
- Verified package includes `UpdateManifest.json`.
- Verified package contains no `Saves/` or `ImportedDatabases/` entries.
- Verified package contains no `SDK launch/build` runtime launch scripts.
