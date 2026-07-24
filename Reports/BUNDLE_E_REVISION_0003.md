# Bundle E Revision 0003 — Movement and AI Disconnect-to-Connect

## DONE

- Added one Bundle E movement/AI closure gate without creating a parallel AI authority.
- Connected validation for AI scheduling, persisted pending-action claims, perception, target selection, threat generation/decay/memory/taunt, assist, flee admission and movement, return-home admission and movement, explicit routes, companion following, teleport following, and companion combat assist.
- Required every movement/AI consumer to be exposed through `RuntimeLivingWorldService`, preserving the existing authoritative living-world persistence owner.
- Corrected the cumulative installer file list so Revision 0002 NPC/spawn model and living-world changes are installed alongside Revision 0003.
- Preserved Bundle B combat, Bundle C inventory/economy, and Bundle D room/topology ownership.

## LEFT

- Followers/pets/companions full lifecycle closure.
- Merchant and trainer full interaction closure.
- Boss, death, reward ownership, respawn, and creature-relationship closure.
- Final duplicate execution-path review and Bundle E completion declaration after all remaining passes.

## VERIFIED

- Explicit runtime target decisions and world-owned monotonic AI ticks.
- Pending AI action persistence and consumer claim ownership.
- Perception, threat selection, generation, decay, memory, taunt, and explicit assist consumers.
- Flee, return-home, and explicit-route admission/completion consumers.
- Companion follow and combat-assist consumers.

## STRONGLY CORROBORATED

- Movement and AI fields execute through the single living-world NPC authority with provenance-preserving metadata.

## DECODED BUT NOT MAPPED

- None added by this pass.

## CONFLICTS

- None detected.

## TRUE UNKNOWN

- Native-only thresholds, route choice, leash distance, morale, and undocumented priority rules remain quarantined and are not invented.

## Files Added

- `Apps/Source/Reforged.Server/RuntimeBundleEMovementAiClosureAudit.cs`
- `Validation/validate_bundle_e_revision_0003.py`
- `Reports/BUNDLE_E_REVISION_0003.md`

## Files Modified

- `Apps/Source/Reforged.Server/RuntimeBundleEExhaustionAudit.cs`
- `INSTALL_AND_VALIDATE.bat`

## Files Removed

- None.

## Duplicate Paths Removed

- None; Bundle H retains major cleanup ownership.

## Build Result

- Requires Windows installer validation; .NET SDK is unavailable in the packaging environment.

## Audit Result

- Static Revision 0003 validation passed.

## Publish Result

- Produced by `INSTALL_AND_VALIDATE.bat` at `Release\MajorMudReforged\Reforged.Server.exe`.
