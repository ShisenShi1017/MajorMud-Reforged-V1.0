# CodeUpdate0193 — NPC Companion Ownership

Implemented exact world-save ownership assignment, inspection, and release for already-created summons, pets, followers, hirelings, escorts, and charmed NPCs. The boundary requires a living NPC, a supported owner kind, a non-empty owner ID, a supported relationship, and explicit authority. Duplicate assignment and mismatched release fail without mutation.

## Not activated

Follow cadence, commands, combat assistance, teleport-follow, zone transfer, duration, dismissal, death, logout/disconnect, charm-break, and native persistence policy remain blocked.
