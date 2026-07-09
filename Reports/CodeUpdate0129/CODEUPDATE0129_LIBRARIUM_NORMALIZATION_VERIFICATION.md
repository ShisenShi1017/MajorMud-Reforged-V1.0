# CodeUpdate0129 — Librarium Normalization & Verification

## Result

**Status:** PASS_WITH_KNOWN_GAPS

This pass redoes CodeUpdate0129 as an audit/normalization milestone instead of another broad placeholder population pass.
It validates the SQL/MDB records imported into the Librarium during CodeUpdate0128 and keeps runtime gameplay unchanged.

## Verified SQL/MDB import coverage

| Family | Expected | Actual | Status |
|---|---:|---:|---|
| rooms | 26666 | 26666 | complete_against_0128_import_metrics |
| items | 2017 | 2017 | complete_against_0128_import_metrics |
| npcs | 1126 | 1126 | complete_against_0128_import_metrics |
| spells | 1458 | 1458 | complete_against_0128_import_metrics |
| shops | 178 | 178 | complete_against_0128_import_metrics |
| classes | 15 | 15 | complete_against_0128_import_metrics |
| races | 13 | 13 | complete_against_0128_import_metrics |


## Relationship audit

- Relationship edges indexed: **66296**
- Valid target edges: **2852**
- Broken/unresolved edges: **63444**

Broken edges are not automatically gameplay failures. Many are references that still need deeper byte/field decoding or point outside the currently selected SQL/MDB gameplay families.
They are now explicit and searchable instead of hidden.

## Evidence linkage

- SQL/MDB record IDs indexed: **31473**
- All Librarium record IDs indexed: **42306**
- Evidence missing count: **0**
- Runtime mapping missing count: **0**

## Open conflicts carried forward

1. Physical Damage Reduction
2. Dodge order
3. Special attack/backstab defense order

## Runtime status

Runtime gameplay code was not modified.

## Policy reaffirmed

- Librarium is authoritative.
- ImportedDatabases are retained until Librarium completion is truly 100% audited.
- /Data is generated runtime output.
- Server/runtime should consume generated /Data, not imported SQL/MDB sources.
