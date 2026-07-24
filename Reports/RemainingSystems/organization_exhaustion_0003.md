# Remaining Systems Pass 0003 — Guilds, Factions, and Organizations

## Executable implementation

`RuntimeOrganizationService` now owns deterministic organization identity, membership, invitations, rank authority, permissions, leadership transfer, member departure, expulsion, disbanding, and guild-recipient routing. Player-created organizations are disabled by the Classic-safe policy unless explicitly enabled by a non-Classic host profile.

## Ownership boundary

This service coordinates organizations. It does not invent dues, shared storage, reputation arithmetic, restricted rooms, guild services, or quest semantics. Those consumers must query organization identity and retain their original subsystem ownership.

## Validation

`--smoke-organizations` exercises creation, rank definition, invitation acceptance, promotion, guild chat routing, leader departure protection, leadership transfer, leaving, disbanding, invariant validation, and fail-closed player creation.

## Evidence status

The Reforged permission and lifecycle shell is proven by executable tests. Its mapping to exact MajorMUD 1.11p organization behavior is strongly correlated and explicitly native-dependent.

## Native dependencies

Exact native organization kinds, player-creation support, rank tables, permissions, persistence, dues, storage, halls, services, quests, reputation, hostility, alliances, commands, and rendered messages remain assigned to the final native pass.
