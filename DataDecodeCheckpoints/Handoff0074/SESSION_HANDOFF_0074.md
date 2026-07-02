# MajorMUD Reforged — Session Handoff 0074

Continue from Patch 0073 / 0074 prep.

Patch 0074 should verify `/Data` against every recovered/imported source in the repo.

## Standing Directives

- `/Data` is runtime source of truth.
- Imported/reference data is evidence only.
- Repo docs are architecture source of truth.
- No duplicate architecture docs unless requested.
- Preserve provenance and unknown fields.
- Future patches are cumulative.

## Recent Work

Patch 0070 promoted MMExplorer MDB room records.
Patch 0071 removed creator-reserved blank placeholder rooms from runtime data while preserving provenance.
Patch 0072/0073 reconciled data and added spawn/occupancy structures.
Patch 0074 is the master verification step.

## User-Specific Notes

- The client app is called The Forge.
- Development/admin/world editing belong on the host/server, not the player client.
- Empty rooms with no exits/descriptions/links are likely creator-reserved map expansion placeholders and should not be runtime rooms.
- World mapping using `.MP` paths should happen after data verification is complete.

## Next Action

Run Patch 0074 verification and produce a cumulative patch ZIP.
