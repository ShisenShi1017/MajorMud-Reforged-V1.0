# CodeUpdate 0111 — Librarium Distribution & SDK Report

## Status

PASS_WITH_LIMITATIONS

## Baseline

CodeUpdate 0110 Scripting & Event Reconstruction cumulative ZIP.

## Repository snapshot

- Source ZIP entries inspected: 5860
- Key folder counts: `{"Codex": 65, "Data": 102, "Documentation": 144, "Evidence": 153, "Generated": 18, "ImportedDatabases": 3890, "Librarium": 180, "Reports": 192, "Runtime": 49, "Sim": 28, "Simulator": 90, "Tools": 163, "Validation": 135}`

## Added distribution artifacts

- Local graph/database builder
- SQLite schema
- Query CLI
- SDK/distribution documentation
- Validation manifest

## Design decision

Generated graph files are not canonical and should not be committed as the primary source of truth. Developers regenerate them locally from canonical Librarium/evidence records.

## Promotions

- Gameplay mechanics promoted: 0
- Formulas promoted: 0
- Runtime behaviors replaced: 0

## Limitations

The builder is a first distribution layer. It creates a useful local database and indexes from repository text artifacts, but deeper semantic extraction can be expanded in later updates.
