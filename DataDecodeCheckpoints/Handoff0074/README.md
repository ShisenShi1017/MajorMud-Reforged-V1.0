# MajorMUD Reforged — Codex Handoff 0074

This package is for updating the repository from the latest cumulative project state and continuing Patch 0074 work.

## Current Baseline

Continue from Patch 0073 / Patch 0074 prep state.

Patch 0074 goal: perform the master verification pass comparing runtime `/Data` against every available recovered/imported reference source in the repo.

## Core Project Rules

- `/Data` is the runtime source of truth.
- `ImportedDatabase/`, WCC/WBB, MMExplorer MDB, HTTrack/Omniverse, and Worldgroup binaries are reference/evidence only.
- Never make the runtime depend directly on original MDB/DAT/WCC/WBB/MDB files.
- Preserve unknown fields and provenance.
- Every discovery must include evidence and confidence.
- All future patch ZIPs are cumulative.
- Repository documentation is the architectural source of truth.
- Do not create duplicate architecture/design docs unless explicitly requested.

## Architecture Reminder

Client:
- The Forge

Host/Server:
- Runtime engine
- Development tools
- Administration
- World editing
- Validation
- Importers

Shared:
- Core gameplay/data models/contracts

## Patch 0074 Work

Perform master verification of `/Data` against:

1. Imported database/reference files
2. WCC/WBB recovered data
3. MMExplorer MDB
4. Worldgroup reverse-engineering evidence
5. Native decode reports
6. HTTrack/Omniverse mirror, if present

Generate reports for:

- Missing items
- Missing monsters
- Missing rooms
- Missing spells
- Missing shops
- Missing skills
- Missing messages
- Duplicate/merged records
- Unknown fields
- Behavior-support fields
- Relationship validation

Relationship validation should include:

- room → exits
- room → monsters
- room → shops
- room → trainers
- monster → loot
- monster → spells/abilities
- spell → effects

## Room Handling Rule

The user believes many empty MDB rooms are creator expansion placeholders.

Runtime `/Data` should exclude rooms that have:

- no exits
- no description
- no links

but they must be preserved in provenance/removed-room manifests.

## Expected Outputs

Patch 0074 should produce:

- Updated source ZIP
- Updated `/Data` if corrections are required
- Master verification report
- CSV comparison reports
- Missing/extra/merged entity reports
- Relationship validation reports
- Confidence matrix
- Updated session handoff
- Updated project directives
- Manifest and SHA256 checksums
- Cumulative Patch 0074 ZIP

## Important Status

Reverse-engineering has largely transitioned from discovery to implementation/validation.

Current confidence estimates:

- Data recovery: 99–100%
- Gameplay formulas: 97–98%
- Worldgroup runtime understanding: about 90%
- Overall gameplay understanding: 97–98%

Known remaining emphasis:

- Verify all data promoted into `/Data`
- Finish semantic validation of spawns/room occupancy
- Preserve unknown flags/opcodes
- Implement and validate gameplay engine behavior after data verification

## Do Not Do

- Do not make runtime load original MDB/WCC/WBB/DAT files.
- Do not overwrite repo architecture docs with duplicate summaries.
- Do not discard unknown fields.
- Do not mark documentation-only evidence as runtime-proven without corroboration.
- Do not promote Omniverse/HTTrack data as authoritative when it conflicts with original data.

