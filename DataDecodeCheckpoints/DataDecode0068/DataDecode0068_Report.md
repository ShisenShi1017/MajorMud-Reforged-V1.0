# DataDecode0068 — Spell Engine Proof

## Scope

DataDecode0068 is a targeted spell subsystem proof pass. It correlates canonical spell records, native WCCSPELS decode evidence, OmniVerse/WCC spell reference fields, uploaded documentation, repository-wide spell terms, and `wgserver.exe` string/constant evidence.

## Verified Inputs

- Repository ZIP: `MajorMud-Reforged-V1.0-main (1).zip`
  - SHA-256: `d79c2e24a07087938919e117d0c5dbfd8afb8cf1812ad5b831c032943454f4de`
  - Files inventoried: 4577
- `wgserver.exe`
  - SHA-256: `4da28d718cf99701aa63c1889770f293bb3ec828aea5e8bea500473d2698c73f`
- Reference MHT files scanned: 10
- Prior local decode outputs were present in the session but this pass derived its tables from current repository and reference evidence.

## Key Metrics

- Canonical spells: 1180
- Native WCCSPELS records: 930
- Native WCCSPELS string rows: 1789
- OmniVerse/WCC spell reference records: 1350
- Canonical spell sourceId to OmniVerse legacy_number matches: 1151
- Canonical spell raw field positions profiled: 41
- Repository files with spell-term hits: 505
- `wgserver.exe` printable strings parsed: 5916
- `wgserver.exe` spell-term string rows: 46
- `wgserver.exe` spell constant probe rows: 439

## Findings

1. Canonical `Data/spells.json` contains 1180 spell records with raw fields preserved for later tuning and proof.
2. Native WCCSPELS decode evidence contains 930 record anchors and 1789 string rows.
3. OmniVerse/WCC spell reference evidence contains 1350 reference spell rows with named fields such as spell level, target type, min/max effect, duration, attack type, resist type, and resist ability.
4. 1151 canonical spell records join to OmniVerse/WCC reference rows through sourceId/legacy_number. This is reference correlation only, not a canonical field promotion.
5. `wgserver.exe` contains constants relevant to spell formulas, but this pass still does not prove the required dataflow chain from routine to native record base pointer to field offset load to arithmetic use.

## Promotion Decision

Field promotions: **0**.

No spell field semantics are promoted from this pass because the strongest missing evidence remains executable dataflow proof. WCC/WBB, OmniVerse, MudInfo, Wiki, and string/constant matches are useful corroboration, but they are not sufficient by themselves.

## Next Recommended Pass

DataDecode0069 should focus on **Spell Executable Dataflow Proof**:

- Identify routines that load WCCSPELS/native spell record bases.
- Trace field offset loads from those base pointers.
- Match those offsets to effect/duration/target/resist candidate fields.
- Confirm arithmetic paths for damage, healing, duration, and MR/anti-magic handling.

