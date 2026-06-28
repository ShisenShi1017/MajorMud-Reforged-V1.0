# Session Handoff

## Current Focus

Milestone 1 — Core Library

## Latest Update

Code Update 0004 — Core Library Foundation

## Current State

The repository layout is frozen. Core now has initial shared source contracts for versioning, paths, logging, validation, editable records, source references, and import auditing.

## Important Rules

- Database Versions apply from their own extracted folders.
- Code Updates apply from their own extracted folders.
- Every update replaces/updates the root README.
- No update overwrites `Saves/` or `ImportedDatabases/`.
- No executable may be claimed unless it actually exists.

## Next Recommended Step

Create the Core data loading and validation services, then use them in the first real server bootstrap.
