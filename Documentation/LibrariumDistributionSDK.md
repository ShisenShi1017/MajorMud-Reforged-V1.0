# CodeUpdate 0111 — Librarium Distribution & SDK

## Purpose

0111 defines the distribution layer for the Librarium. The project cannot rely on committing giant generated graphs to GitHub, so the repository now treats graph/database/index files as reproducible local artifacts.

## Canonical source

The canonical source remains:

- `/Librarium`
- `/Evidence`
- `/Validation`
- `/Reports`
- `/Documentation`
- `/Codex`

Generated artifacts are derived from those records.

## Local outputs

Running the builder creates:

- `Generated/Librarium/librarium.sqlite`
- `Generated/Librarium/core_graph.json`
- `Generated/Librarium/query_index.json`
- `Generated/Librarium/runtime_index.json`
- `Generated/Librarium/evidence_index.json`

These outputs are intentionally reproducible and should not become independent game truth.

## SDK contract

Consumers should ask the Librarium SDK/database for records, relationships, evidence, and runtime mappings. They should not invent separate truth models.

## Evidence impact

This update does not promote gameplay mechanics or formulas. It promotes the distribution workflow: source records in Git, generated graph/database locally.
