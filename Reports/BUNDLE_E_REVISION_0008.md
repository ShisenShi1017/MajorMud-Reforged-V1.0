# Bundle E Revision 0008 — NPC Item, Control, and Persistence Disconnect-to-Connect

## DONE
Connected NPC inventory ownership, exact equipment ownership and selection, ammunition consumption, item use, item-effect invocation, decoded regeneration/charm/follow fields, and NPC persistence validation into one mandatory Bundle E closure gate.

## LEFT
Final Bundle E exhaustion review: reconcile every remaining decoded creature field, validate duplicate execution paths, classify all unresolved relationships, and produce the Bundle E completion package only if every Verified or Strongly Corroborated mechanic is consumed.

## VERIFIED
NPC item transfers use Bundle C item authority; equipment, ammunition, item use/effects, and persistence have live runtime smoke consumers. Decoded regeneration and charm/follow fields load through the authoritative Classic NPC catalog.

## STRONGLY CORROBORATED
Evidence-driven equipment and item-use admission are connected without inventing autonomous selection behavior.

## DECODED BUT NOT MAPPED
No verified autonomous consumable-priority formula was found beyond existing evidence-backed admission.

## CONFLICTS
None introduced.

## TRUE UNKNOWN
Native-only regeneration edge timing and charm command nuances remain quarantined for the final native pass.

## Files Added
RuntimeBundleENpcItemControlPersistenceClosureAudit.cs; validate_bundle_e_revision_0008.py; this report.

## Files Modified
Program.cs; RuntimeBundleEExhaustionAudit.cs; INSTALL_AND_VALIDATE.bat.

## Files Removed
None.

## Duplicate Paths Removed
None; exact ownership boundaries are validated without Bundle H cleanup.
