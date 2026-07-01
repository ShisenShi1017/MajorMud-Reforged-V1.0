# CodeUpdate-0047 DataDecode GitHub Transition Report

## Summary

CodeUpdate-0047 moves decode continuity into the repository as the authoritative working copy.

This update preserves the user-provided DataDecode0049 checkpoint, records the DataDecode0050 GitHub transition handoff, and updates continuity documents so future decode passes are committed directly to the repository instead of being developed as independent working ZIPs.

## Preserved Evidence

DataDecode0049 source package:

- Path: `C:\Users\djkao\Downloads\DataDecode0049-WGSERVERRecordAccessStructureCorrelation-PreMirror.zip`
- SHA-256: `8869C12860A5D645F5EBA5B8F6F51C9389A98E96A3ABA7B8CBB57288CCCE8A2B`
- Preserved under: `DataDecodeCheckpoints/DataDecode0049/`
- File count: 17.

WGSERVER reference executable:

- Path: `C:\Worldgroup Server\wgserver.exe`
- Size: 432,128 bytes.
- SHA-256: `4DA28D718CF99701AA63C1889770F293BB3EC828AEA5E8BEA500473D2698C73F`
- Not copied or executed by this update.

## DataDecode0049 Findings

- 15 NativeRuntime DAT datasets profiled.
- 5,916 executable ASCII strings length 4+.
- 9,574 record-size/count/file-size literal hits.
- 29 string-address cross references.
- 29 record-access candidate windows.
- 15 dataset record descriptor candidate rows.
- 93 canonical term correlation hits.
- 0 field promotions.

## DataDecode0050 Transition

The user-provided handoff says the workflow should stop using checkpoint ZIPs as independent working repositories. Future decode passes should update the GitHub repository directly and may export ZIPs only as archival/checkpoint artifacts.

No standalone DataDecode0050 ZIP was supplied in this Codex turn. DataDecode0050 dispatcher/message items are recorded as handoff continuity only until supported by committed reports or binary-backed evidence.

## Boundaries

- No canonical `Data/` rewrite.
- No field promotions.
- No server behavior changes.
- No gameplay systems.
- No `ImportedDatabases/` deletion.
- No execution or copying of `wgserver.exe`.
- No OmniVerse-to-Data overwrite.
- No assumption that OmniVerse IDs equal binary row indexes.

## Next Recommended Decode Pass

DataDecode0051 - Combat Routine Correlation:

- Damage calculations.
- Armor calculations.
- Spell calculations.
- Resistance calculations.
- Ability calculations.

Every promoted field must be supported by binary evidence.
