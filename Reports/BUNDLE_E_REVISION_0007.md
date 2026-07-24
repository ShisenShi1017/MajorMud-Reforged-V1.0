# Bundle E Revision 0007 — Relationship/Service Disconnect-to-Connect

## DONE
- Connected decoded NPC alignment through RuntimeNpcDefinition into living NPC metadata and persisted disposition.
- Enforced decoded disposition routing: service/shop/trainer NPCs friendly, decoded evil alignment hostile, remaining decoded alignment neutral.
- Verified generated spawn-group relationships load through RuntimeClassicNpcAiCatalog and persist on living NPC instances.
- Connected explicit-hostility perception and explicit assist authorization to the single living-world NPC authority.
- Connected dialogue, NPC service admission/completion, and service-refusal dialogue metadata persistence.
- Added a mandatory relationship/service closure gate to the Bundle E master exhaustion audit.
- Verified decoded/imported sources: Classic generated data, NPC reconstruction catalogs, Librarium, MMExplorer SQL imports, MDB, OmniVerse, and WCC references.
- Verified single authority paths and no duplicate relationship/service runtime owners.

## LEFT
- Native faction membership, reputation thresholds, automatic enemy matrices, and guard-protection selection remain relationship-unresolved because no verified decoded mapping exists. They remain quarantined for the final native pass rather than invented.

## VERIFIED
- Alignment/disposition persistence.
- Spawn-group relationship ownership.
- Dialogue and service admission/completion persistence.
- Service refusal routing.

## STRONGLY CORROBORATED
- Explicit-hostility target admission and explicit assist authorization boundaries.

## DECODED BUT NOT MAPPED
- None for the relationship/service fields completed in this revision.

## CONFLICTS
- None detected.

## TRUE UNKNOWN
- Native-only faction, enemy, and guard formulas.

## Files Added
- Apps/Source/Reforged.Server/RuntimeBundleERelationshipServiceClosureAudit.cs
- Validation/validate_bundle_e_revision_0007.py
- Reports/BUNDLE_E_REVISION_0007.md

## Files Modified
- Apps/Source/Reforged.Server/RuntimeBundleEExhaustionAudit.cs
- Apps/Source/Reforged.Server/Program.cs
- INSTALL_AND_VALIDATE.bat

## Files Removed
- None.

## Duplicate Paths Removed
- None; no duplicate authoritative paths were found.

## Build Result
- Deferred to Windows installer environment.

## Audit Result
- Static validation: 40 passed, 0 failed.

## Publish Result
- Deferred to Windows installer environment.

> **Superseded by Revision 0010:** Classic has no creature factions or reputation. Hostile NPCs do not attack each other. Guarding is explicit same-room protector attack blocking.
