# Bundle E Revision 0004 — Companion Lifecycle Disconnect-to-Connect

## DONE
- Added one mandatory closure gate for followers, pets, companions, summons, hirelings, escorts, and charmed NPCs.
- Connected validation across summon materialization/policy, ownership assignment and release, owner commands, ordinary and teleport following, combat assistance, ownership transfer, death/dismissal/expiry/logout/disconnect cleanup, and living-world persistence metadata.
- Main Bundle E exhaustion audit now fails when any companion lifecycle consumer or authority path is missing or duplicated.
- Added a dedicated runtime audit command and cumulative installer coverage.

## LEFT
- Merchants and trainers disconnect-to-connect closure.
- Boss orchestration, death/credit/reward ownership, and respawn timer closure.
- Creature faction/guard/enemy relationships and final Bundle E field exhaustion.

## VERIFIED
Ownership, commands, follow completion, combat assistance, release, cleanup, and persistence consumers.

## STRONGLY CORROBORATED
Pet, hireling, escort, charm, and summon relationship variants use the common companion authority while relationship-specific native formulas remain evidence-gated.

## DECODED BUT NOT MAPPED
None introduced by this pass.

## CONFLICTS
None. Bundle B remains combat authority; Bundle E owns companion behavior and authorization.

## TRUE UNKNOWN
Native-only companion duration, charm, hireling fee, and relationship-specific formulas without decoded evidence remain quarantined.

## Files Added
- Apps/Source/Reforged.Server/RuntimeBundleECompanionLifecycleClosureAudit.cs
- Validation/validate_bundle_e_revision_0004.py
- Reports/BUNDLE_E_REVISION_0004.md

## Files Modified
- Apps/Source/Reforged.Server/RuntimeBundleEExhaustionAudit.cs
- Apps/Source/Reforged.Server/Program.cs
- INSTALL_AND_VALIDATE.bat

## Files Removed
None.

## Duplicate Paths Removed
None; the audit enforces a single public living-world lifecycle authority.

## Build Result
Run INSTALL_AND_VALIDATE.bat on Windows.

## Audit Result
Static package validation passed; live audit is installer-enforced.

## Publish Result
Installer publishes to Release\MajorMudReforged and verifies Reforged.Server.exe.
