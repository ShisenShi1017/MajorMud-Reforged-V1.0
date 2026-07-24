# Items Runtime Semantic Exhaustion — CodeUpdate0168

## Implemented

- Added explicit bound-owner enforcement for ownership-changing character and NPC transfers.
- Added explicit quest-owner enforcement for ownership-changing character and NPC transfers.
- Added carried-state and projected no-trade enforcement before trade staging.
- Added owner-validated ordinary item destruction.
- Added projected no-destroy and quest-protected destruction rejection.
- Added explicit administrative removal with an auditable override result.
- Preserved non-empty portable-container destruction rejection.
- Added save-state validation for projected owner restriction metadata and owned-location mismatches.
- Added deterministic rollback and lifecycle smoke coverage.

## Evidence boundary

This pass activates only explicit instance metadata. It does not map any unknown Classic flag bit, infer quest ownership from item names, or establish native administrator permissions. Administrative removal is Reforged compatibility infrastructure and must be permission-gated by the calling administration subsystem.

## Remaining

- Decode native binding, quest, no-trade, no-sell, no-bank, and no-destroy flag bits.
- Recover exact Classic exception paths for quests, death, corpses, resets, deletion, and sysop commands.
- Separate native trade, merchant-sale, bank, guild-storage, and quest-handoff eligibility.
- Recover limited-item reservation release and duplicate reconciliation during destruction.
- Recover container-content outcomes for native destruction and breakage.
