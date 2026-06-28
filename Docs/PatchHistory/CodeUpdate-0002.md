# Code Update 0002

Name: Repository Deduplication

Purpose: Remove redundant prototype and duplicate files from the active repository while preserving the current streamlined architecture.

Apply file: `Apply-CodeUpdate-0002-Repository-Deduplication.bat`

Safety:

- Does not remove `Data/`.
- Does not remove `ImportedDatabases/`.
- Does not remove `Saves/`.
- Does not delete `.git`.
