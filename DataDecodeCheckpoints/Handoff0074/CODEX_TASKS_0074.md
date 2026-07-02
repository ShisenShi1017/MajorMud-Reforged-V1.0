# Codex Tasks — Patch 0074 Master Verification

## 1. Locate Runtime Data

Inspect `/Data` and summarize all runtime JSON datasets.

Expected major files include:

- items.json
- monsters.json
- rooms.json
- world.json
- room_graph.json
- spells.json
- shops.json
- races.json
- classes.json
- skills.json
- loot_tables.json
- trainers.json
- spawn_tables.json
- room_occupancy.json
- messages.json or message catalogs

## 2. Locate Reference Evidence

Search repo for imported/reference data folders and reports, including likely names:

- ImportedDatabase
- ImportedDatabases
- NativeDatDecode
- NativeDatDecodeReports
- NativeDecodeReports
- NativeRuntimeCatalog
- MMExplorer
- HTTrack
- Omniverse
- WCC
- WBB
- Worldgroup

Do not use these as runtime dependencies.

## 3. Count Comparison

Produce a count comparison CSV:

- dataset
- runtime_count
- reference_count
- matched_count
- missing_count
- extra_count
- confidence
- notes

## 4. Entity Identity Comparison

For each available entity type, compare IDs and names:

- items
- monsters
- rooms
- spells
- shops
- races
- classes
- skills
- messages

Produce missing/extra/renamed/duplicate CSVs.

## 5. Field-Level Comparison

Where reference fields can be mapped, compare:

- names
- descriptions
- stats
- flags
- restrictions
- links/references
- provenance

Use confidence labels:

- verified
- verified_with_variance
- reference_only
- runtime_only
- preserved_unknown
- unresolved

## 6. Relationship Validation

Validate references and produce reports for:

- room exits to known rooms
- room occupancy to known rooms/monsters
- spawn tables to known rooms/monsters
- loot tables to known monsters/items
- shops to known items/rooms
- trainers to known rooms/classes/skills
- spells to known effect IDs or preserved unknowns

## 7. Apply Safe Corrections Only

Only update `/Data` when:

- evidence is traced to original imported/recovered data
- layout follows existing `/Data` conventions
- provenance is preserved
- unknown source fields are retained or referenced

Otherwise, report the gap without modifying runtime data.

## 8. Produce Patch Materials

Create:

- DATA_MASTER_VERIFICATION_0074_REPORT.md
- data_master_count_comparison_0074.csv
- missing_entities_0074.csv
- extra_entities_0074.csv
- duplicate_merged_entities_0074.csv
- relationship_validation_0074.csv
- unknown_fields_0074.csv
- behavior_support_data_0074.csv
- SESSION_HANDOFF_0074.md
- CHANGELOG_0074.md
- PATCH_0074_MANIFEST.md
- PATCH_0074_MANIFEST.csv
- SHA256SUMS.txt

Package everything as a cumulative patch ZIP.
