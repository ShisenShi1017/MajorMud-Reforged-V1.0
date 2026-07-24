# Bundle D Revision 0009 — Phase D7 Hidden Object Rules

## DONE
- Exhausted decoded hidden-exit records from generated Classic data and MMExplorer room decode.
- Distinguished decoded `Hidden/Searchable`, `Hidden/Passable`, and hidden action-latch modes.
- Hidden exits are omitted from obvious-exit and directional-look output until discovered by that character.
- `Hidden/Searchable` traversal is denied until that character discovers the canonical generated exit ID.
- `Hidden/Passable` permits explicit direction traversal and records player-specific discovery; automatic routing remains unavailable until discovery.
- Search now stores canonical generated exit IDs rather than synthesizing a parallel exit identity.
- Existing cleanup-hidden item rules remain authoritative: player-specific discovery, hidden-on-ground after discovery, pickup removes hidden state, re-drop is visible, and hidden/visible stacks do not merge.
- Added executable D7 hidden-object audit and installer gate.

## LEFT
- D8 Teleporters.
- Hidden action-count/specific-order latches continue into their decoded action/trap consumer pass; no action sequence was invented in D7.

## VERIFIED
- Hidden ground-object discovery is player-specific.
- Discovery does not globally unhide an object or exit.
- Cleanup-hidden items become visible only after pickup and re-drop.
- Canonical generated exit IDs own hidden-exit discovery.

## STRONGLY CORROBORATED
- Generated hidden-exit edge-state semantics define player knowledge as owner for Searchable/Passable modes.

## DECODED BUT NOT MAPPED
- Hidden action-latch execution remains assigned to the action/trap phase; decoded count and order remain preserved.

## CONFLICTS
- None introduced. The earlier generic treatment of every hidden exit as Searchable is removed.

## TRUE UNKNOWN
- No numeric concealment threshold is present on decoded Hidden/Searchable exit records. No threshold is invented.

## Files Added
- Apps/Source/Reforged.Server/RuntimeBundleDHiddenObjectAudit.cs
- Librarium/Reports/bundle_d_revision_0009.md

## Files Modified
- Apps/Source/Reforged.Server/RuntimeBundleDSearch.cs
- Apps/Source/Reforged.Server/RuntimeLiveWorld.cs
- Apps/Source/Reforged.Server/RuntimeLivingWorld.cs
- Apps/Source/Reforged.Server/RuntimeLivingWorldCompletion.cs
- Apps/Source/Reforged.Server/Program.cs
- INSTALL_AND_VALIDATE.bat

## Files Removed
- None.

## Duplicate Paths Removed
- Synthesized hidden-exit discovery IDs were replaced by canonical generated exit IDs.

## Build Result
- Local .NET SDK unavailable; installer performs warning-as-error win-x64 build.

## Audit Result
- Static decoded-data validation passed. Executable D7 audit is included in installer.

## Publish Result
- Installer publishes to Release/MajorMudReforged and verifies Reforged.Server.exe.
