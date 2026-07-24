# Bundle E Revision 0010 — Classic Relationship Correction

## DONE
- Removed the incorrect Bundle E assumption that creature factions, creature reputation, automatic enemy matrices, or broad guard formulas were unresolved Classic mechanics.
- Enforced the verified Classic rule that hostile NPCs do not target or attack other NPCs.
- Added the authoritative explicit protector-to-protected-NPC attack-admission boundary.
- A living protector in the same room moves between the player and its explicitly protected NPC and blocks the requested attack.
- Removed protector/guard metadata from generic NPC assist authorization so protection cannot become NPC-vs-NPC combat.
- Updated Bundle E relationship, exhaustion, and zero-disconnect audits.

## LEFT
- None for this correction pass.

## VERIFIED
- Classic creature runtime has no faction system.
- Classic creature runtime has no reputation system.
- Hostile NPCs do not attack each other.
- Guarding is an explicit same-room protector relationship that blocks attacks against a designated NPC.

## STRONGLY CORROBORATED
- Existing explicit assist relationships remain separate from protector attack blocking.

## DECODED BUT NOT MAPPED
- None introduced by this correction.

## CONFLICTS
- Revision 0007/0009 quarantine wording was incorrect and is superseded by Revision 0010.

## TRUE UNKNOWN
- No faction, reputation, enemy-matrix, or broad guard formula remains classified as unknown.

## Files Added
- Apps/Source/Reforged.Server/RuntimeNpcProtectionAdmission.cs
- Validation/validate_bundle_e_revision_0010.py
- Reports/BUNDLE_E_REVISION_0010.md

## Files Modified
- RuntimeLiveCombat.cs
- RuntimeLiveCombatOwnership.cs
- RuntimeNpcPerception.cs
- RuntimeNpcThreat.cs
- RuntimeBundleERelationshipServiceClosureAudit.cs
- RuntimeBundleEExhaustionAudit.cs
- RuntimeBundleEZeroDisconnectClosureAudit.cs
- INSTALL_AND_VALIDATE.bat

## Build Result
- Requires Windows installer execution.

## Audit Result
- Static correction validation passed.

## Publish Result
- Requires Windows installer execution.
