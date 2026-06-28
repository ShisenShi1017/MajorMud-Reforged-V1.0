# Core Update 0015 - Data Cross-Reference Validation

## Purpose

Continue Milestone 1 Core Framework work by expanding Data JSON validation from individual file health checks into relationship validation across the live `Data/` database.

## Added

- `Core/Source/MajorMud.Core/Health/DataCrossReferenceHealthCheck.cs`
  - Indexes current room, item, monster, class, race, shop, quest, loot-table, and world room records.
  - Validates world room exits against known room IDs.
  - Validates world room item and monster references against known item and monster IDs.
  - Validates race `availableClasses` values against known classes, while preserving the existing `all` shortcut.
  - Adds future-ready checks for shop, quest, and loot-table references as those files become populated.
- Source-level `ValidationResult.Merge(...)` support so health checks can compose without application dependencies.
- `DataJsonHealthCheck` integration so cross-reference validation runs after parse/root/duplicate-ID checks pass.

## Runtime Policy

This update adds source-level Core validation only. It does not add a normal-runtime dependency on the .NET SDK and does not claim compiled `Bin/` artifacts exist.

## Protected Data

This update does not modify `Saves/` or `ImportedDatabases/`.
