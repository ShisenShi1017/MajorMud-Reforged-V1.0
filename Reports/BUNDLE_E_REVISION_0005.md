# Bundle E Revision 0005 — Merchant and Trainer Disconnect-to-Connect

## DONE

Connected the complete merchant and trainer runtime chain through one mandatory Bundle E closure gate:

- decoded/imported WCC shop data, MDB shop records, Librarium catalogs, simulator references, OmniVerse references, and NPC AI evidence;
- living merchant/trainer NPC service admission and completion;
- merchant stock ownership, exact stock mutation, access, restock admission, and merchant-gold settlement;
- trainer eligibility, persistent offering ownership, offering admission, cooldown completion, and teaching-service routing;
- persistent shop and trainer state transactions;
- Bundle C merchant lifecycle, item identity, inventory, currency, price, and settlement ownership boundaries;
- duplicate-authority checks and the main Bundle E runtime exhaustion audit.

## LEFT

Boss lifecycle, death/kill/reward ownership, respawn/reset/hard-timer closure, then creature relationship and final Bundle E field exhaustion review.

## VERIFIED

Merchant stock/access/restock/settlement and trainer eligibility/offering/admission consumers are present and required by the live Bundle E audit.

## STRONGLY CORROBORATED

Trainer dialogue and offering routing remain connected through the generic living NPC service boundary without inventing native-only refusal text.

## DECODED BUT NOT MAPPED

None in the merchant/trainer slice completed by this revision.

## CONFLICTS

None. Bundle C remains economy authority; Bundle E owns NPC behavior and service routing.

## TRUE UNKNOWN

Native-only merchant/trainer semantics without decoded evidence remain quarantined for the final native pass.

## Files Added

- Apps/Source/Reforged.Server/RuntimeBundleEMerchantTrainerClosureAudit.cs
- Validation/validate_bundle_e_revision_0005.py
- Reports/BUNDLE_E_REVISION_0005.md

## Files Modified

- Apps/Source/Reforged.Server/RuntimeBundleEExhaustionAudit.cs
- Apps/Source/Reforged.Server/Program.cs
- INSTALL_AND_VALIDATE.bat

## Files Removed

None.

## Duplicate Paths Removed

None removed; the validator enforces one authoritative source file for each merchant/trainer execution boundary.

## Build Result

Run by INSTALL_AND_VALIDATE.bat on the Windows repository with warnings treated as errors.

## Audit Result

Static validation completed in package assembly. Live merchant/trainer and full Bundle E audits run from the installer.

## Publish Result

Installer publishes to Release\MajorMudReforged and verifies Reforged.Server.exe.
