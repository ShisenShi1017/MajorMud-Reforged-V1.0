# Librarium MMExplorer Textblock Quest Update 0142

This update replaces the incomplete 0141 textblock grammar decoder.

## Results

- 2,279 textblocks decoded
- 771 NPC-to-textblock links
- 83 quest flows
- 9,322 relationship edges
- 0 decode errors
- 0 unknown opcodes
- 0 unclassified tokens
- 0 unresolved source directives
- 6 tests pass

## Decoder changes

The parser now distinguishes:

- command-map routes (`phoenix:1429`)
- player command triggers (`use crowbar`)
- numeric selection triggers (`50`)
- conditions and guards
- effects and mutations
- dialogue branches and messages
- legacy aliases such as `add delay`

Two malformed source forms are repaired with explicit provenance:

1. Textblock 237: lone numeric segment `0` is emitted as a numeric route.
2. Textblock 9578: `2683 1` is emitted as `teleport 2683 1` because three sibling portal lines in the same textblock use the identical operation explicitly.

The original raw text remains unchanged in every record. Repairs are recorded under `source_repair`.

## Install

Extract over the repository root.

Regenerate:

```bash
python Tools/MDBExtractor/decode_mmexplorer_textblocks.py \
  ImportedDatabases/data-v1.11n.mdb \
  --repo-root . \
  --outdir Librarium/SourceImports/MMExplorerSQLData/TextblocksDecoded0142
```

Validate:

```bash
python Tools/Validation/validate_mmexplorer_textblock_update_0142.py .
python -m pytest -q Tools/MDBExtractor/tests/test_decode_mmexplorer_textblocks.py
```

Librarium remains authoritative. These files are source-import records for canonical promotion; do not copy them directly to `Data/Classic`.
