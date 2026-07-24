# Bundle E Revision 0006 — Boss, Death and Respawn Disconnect-to-Connect

## DONE
Connected the existing decoded/imported boss, NPC death, corpse, loot ownership and respawn paths through one mandatory runtime closure audit. The gate executes combat death fan-out validation, corpse ownership/access smoke paths, exact loot materialization, Bundle C loot rights, spawn/respawn lifecycle contracts, blocked retry, spawn-slot reset, and imported evidence checks.

## LEFT
Creature relationships, factions, guard/enemy/assist disposition and remaining service/dialogue closure; then final Bundle E field matrix and duplicate execution review.

## VERIFIED
Death fan-out, corpse transfer/access, loot materialization, reward rights boundary, respawn wait/tick, blocked retry and reset interaction.

## STRONGLY CORROBORATED
Boss unique occupancy and quest/boss death routing through existing spawn and death event identities.

## DECODED BUT NOT MAPPED
None introduced by this revision.

## CONFLICTS
None. Bundle B/C/D ownership boundaries remain unchanged.

## TRUE UNKNOWN
Native-only semantics without decoded evidence remain quarantined for the final native pass.

## Files Added
- Apps/Source/Reforged.Server/RuntimeBundleEBossDeathRespawnClosureAudit.cs
- Validation/validate_bundle_e_revision_0006.py
- Reports/BUNDLE_E_REVISION_0006.md

## Files Modified
- Apps/Source/Reforged.Server/Program.cs
- Apps/Source/Reforged.Server/RuntimeBundleEExhaustionAudit.cs
- INSTALL_AND_VALIDATE.bat

## Files Removed
None.

## Duplicate Paths Removed
None removed; the audit enforces singular authoritative source paths.

## Build Result
Performed by INSTALL_AND_VALIDATE.bat with warnings as errors.

## Audit Result
Static and live boss/death/respawn closure audits required.

## Publish Result
Installer publishes Release\MajorMudReforged\Reforged.Server.exe.
