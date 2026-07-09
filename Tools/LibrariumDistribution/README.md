# Librarium Distribution & SDK

This package defines how other developers consume the Librarium without committing huge generated graph files to GitHub.

## Core rule

`/Librarium`, `/Evidence`, and related source records are canonical. Generated graphs, indexes, and databases are local build artifacts.

## Build locally

From the repository root:

```bash
python Tools/LibrariumDistribution/build_librarium_distribution.py
```

This creates:

```text
Generated/Librarium/librarium.sqlite
Generated/Librarium/core_graph.json
Generated/Librarium/query_index.json
Generated/Librarium/runtime_index.json
Generated/Librarium/evidence_index.json
```

These files should normally remain uncommitted.

## Query locally

```bash
python Tools/LibrariumDistribution/librarium_query.py search combat
python Tools/LibrariumDistribution/librarium_query.py record <record_id>
python Tools/LibrariumDistribution/librarium_query.py refs <record_id>
```

## Consumer model

Runtime, editor, simulator, documentation, and AI tools should consume the generated database/indexes through this SDK layer instead of parsing unrelated files independently.
