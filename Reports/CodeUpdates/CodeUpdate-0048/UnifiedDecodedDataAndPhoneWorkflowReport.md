# CodeUpdate-0048 Unified Decoded Data and Phone Workflow Report

## Summary

CodeUpdate-0048 creates a repo-native unified decoded-data index and comparison surface.

It also adds an Android ChatGPT decode workflow so phone-based decode work can produce consistent handoffs without PowerShell, local scripts, or direct filesystem access.

## Generated Unified Data Folder

Path:

`DataDecodeCheckpoints/UnifiedDecodedData/`

Generated files:

- `UnifiedDecodedDataManifest.json`
- `README.md`
- `Inventory/DecodedSourceInventory.csv`
- `Summary/OmniVerseComparisonSummary.csv`
- `Comparisons/Classes_NameCoverage.csv`
- `Comparisons/Races_NameCoverage.csv`
- `Comparisons/Items_NameCoverage.csv`
- `Comparisons/Spells_LegacyIdCoverage.csv`
- `Comparisons/Monsters_NameCoverage.csv`

The folder indexes current canonical data, decode checkpoints, native decode reports, and the CodeUpdate0046 OmniVerse/MudInfo reference payload. It does not move or overwrite historical reports.

## Comparison Snapshot

- Source inventory rows: 1,653.
- Canonical classes: 15; OmniVerse classes: 15; normalized-name matches: 15.
- Canonical races: 13; OmniVerse races: 13; normalized-name matches: 13.
- Canonical skills: 46; OmniVerse abilities: 188; not equivalent yet.
- Canonical items: 1,770; OmniVerse items: 1,950; 1,948 OmniVerse items have a normalized name present in canonical data. This is name-only correlation because canonical items lack `legacy_number`.
- Canonical spells: 1,180; OmniVerse spells: 1,350; 1,151 matched by canonical `sourceId` to OmniVerse `legacy_number`; 199 OmniVerse spells were not present by that match.
- Canonical shops: 0; native decoded shop records: 145; OmniVerse shops: 178.
- Canonical monsters: 98 starter/sample records; OmniVerse monsters: 1,100; 50 normalized-name matches.
- Canonical rooms: 0; OmniVerse room details: 1,246; OmniVerse roommaps: 168.

## Android ChatGPT Workflow

Added:

- `Docs/DATDecoding/AndroidChatGPTDecodeWorkflow.md`
- `DataDecodeCheckpoints/PhoneHandoffTemplates/README.md`
- `DataDecodeCheckpoints/PhoneHandoffTemplates/DataDecodePhoneHandoffTemplate.md`

Phone ChatGPT work should produce structured handoffs only. It is not repository truth until Codex verifies and commits it.

## Boundaries

- No canonical `Data/` rewrite.
- No field promotions.
- No server behavior changes.
- No gameplay systems.
- No deletion of imported/reference data.
- No OmniVerse-only promotion.
- No U16/U32-only promotion.

## Next Recommended Work

After user push and green GitHub Actions, continue DataDecode0051 Combat Routine Correlation from the GitHub repository working copy.
