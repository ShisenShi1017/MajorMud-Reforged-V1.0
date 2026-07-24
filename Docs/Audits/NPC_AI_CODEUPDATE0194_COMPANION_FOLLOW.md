# CodeUpdate0194 — NPC Companion Follow Admission and Completion

Implemented an exact, save-safe follow boundary for NPCs whose companion ownership was already assigned by CodeUpdate0193.

## Runtime behavior

- Requires a living materialized NPC and an exact stored owner-kind/owner-ID match.
- Requires an explicit leader room and an identical destination room.
- Requires explicit reachability and a canonical generated destination.
- Persists one exact pending follow destination and whether the owning subsystem classified it as teleport-follow.
- Commits room movement only after exact source, destination, owner, and traversal-completion matching.
- Rejected completion leaves pending follow state intact for exact retry.

## Deliberately unresolved

The runtime does not derive follow cadence, path selection, teleport eligibility, zone rules, movement costs, disconnect cleanup, or native companion pathing.
