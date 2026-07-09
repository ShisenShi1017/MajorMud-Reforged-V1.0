# CodeUpdate 0116 - Librarium Runtime Conversion

## Decision

The Librarium is the authoritative source for game knowledge, runtime definitions, host-editable world design, client read-only views, and evidence-backed Classic rules.

`/Data` is generated runtime output per world. It is no longer the project source of truth.

## Flow

```text
Librarium
  Articles / evidence / conflicts / runtime definitions / world overrides
        ↓
Validate and export
        ↓
/Data/Classic or /Data/Worlds/<world>
        ↓
Server runtime
        ↓
Client read-only world-specific Librarium/Data view
```

## World policy

- Classic: locked, evidence-backed, not host editable except through verified Librarium updates.
- Custom: host editable through validated world overrides.
- AI: host editable through validated AI constraints and world overrides.

## Cleanup

Legacy generated `/Data`, `/Generated`, `/DataDecodeCheckpoints`, and old `/ImportedDatabases` snapshots were removed from the cleaned repo. Canonical GitHub source evidence now lives under `Evidence/CanonicalSources`.

## Current limitation

CodeUpdate 0116 adds the authoritative structure and export scaffold. Runtime code still needs the next pass to consume generated `/Data/<World>` packages consistently.
