# DataDecode0068 Repository Preservation

This folder preserves the user-provided `DataDecode0068-SpellEngineProof.zip` package in the repository.

## Source Package

- Source path: `C:\Users\djkao\Downloads\DataDecode0068-SpellEngineProof.zip`
- ZIP SHA-256: `587B92CE0495BF20C0619E4F2139CAA980F17A2EAE70A8C36A7A4162374CAFE6`
- Preserved payload file count: 19 package files, plus this README.

## Summary

DataDecode0068 is a targeted spell subsystem proof pass. It correlates canonical spell records, native WCCSPELS decode evidence, OmniVerse/WCC spell reference fields, uploaded documentation, repository-wide spell terms, and `wgserver.exe` string/constant evidence.

Key reported metrics:

- Canonical spell records: 1,180.
- Native WCCSPELS records: 930.
- Native WCCSPELS string rows: 1,789.
- OmniVerse/WCC spell reference records: 1,350.
- Canonical `sourceId` to OmniVerse `legacy_number` spell matches: 1,151.
- Canonical spell raw field positions profiled: 41.
- Reference MHT files scanned: 10.
- Repository files with spell-term hits: 505.
- WGSERVER printable strings parsed: 5,916.
- WGSERVER spell-term string rows: 46.
- WGSERVER spell constant probe rows: 439.
- Field promotions: 0.

## Decision

No canonical spell field semantics are promoted by this checkpoint. The missing proof remains executable dataflow:

`spell routine -> native spell record base pointer -> field offset load -> arithmetic/branch use -> observed spell behavior`

## Next Recommended Decode Pass

DataDecode0069 should focus on Spell Executable Dataflow Proof:

- Identify routines that load WCCSPELS/native spell record bases.
- Trace field offset loads from those base pointers.
- Match offsets to effect, duration, target, and resist candidate fields.
- Confirm arithmetic paths for damage, healing, duration, and MR/anti-magic handling.
