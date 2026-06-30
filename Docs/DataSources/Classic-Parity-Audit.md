# Classic Parity Audit

## Goal

The Classic parity audit measures how close Reforged is to reproducing the Classic MajorMUD baseline.

`Data/` remains the canonical editable game database. `ImportedDatabases/` remains reference/import material only. Imported sources help identify gaps and possible corrections, but they do not automatically overwrite canonical data.

All WCC data, including WCC default installs, WCC-labeled WBB material, WSGSERVE-related files, and WCC-derived archives, is reference-only unless the user explicitly approves a reviewed import/reconciliation update.

## Current Scope

CodeUpdate-0041 provides an inventory foundation only.

It compares file/category presence across:

- Canonical `Data/`
- Imported/reference material under `ImportedDatabases/`

The audit classifies broad content categories:

- Rooms
- Monsters
- Items
- Spells
- Shops
- Quests
- Classes/Races
- Ganghouses
- Maps/Paths
- Raw legacy database files

## What It Does Not Do Yet

- It does not parse proprietary binary formats.
- It does not decode WCC/WBB/WSGSERVE records.
- It does not rewrite canonical `Data/`.
- It does not calculate exact MajorMUD parity.
- It does not enable gameplay.
- It does not promote WCC reference material into live server data.

## Intended Evolution

Future follow-up updates should add narrow parsers and field comparisons one category at a time.

Suggested sequence:

1. Inventory all canonical and imported/reference sources.
2. Add source-specific parsers for known formats.
3. Map source records to canonical data models.
4. Compare IDs, names, stats, flags, links, drops, prices, requirements, and text.
5. Generate category coverage reports.
6. Produce an overall Classic reproduction score only after field-level comparison exists.

## Reporting Principle

Do not claim Reforged is faithful to Classic unless the claim is backed by parsed comparison evidence.
