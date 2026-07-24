# Bundle D Revision 0005 — Phase D5 Search System

## DONE
- Registered the live `search` command in the authoritative command dispatcher.
- Routed cleanup-hidden room items through one player-specific discovery registry.
- Corrected the shared ground-item loader so `look`, `examine`, and item targeting cannot expose undiscovered hidden items.
- Connected decoded hidden exit gates and hidden NPC metadata to the same search target model.
- Consumed Bundle A See Hidden rank as a passive discovery capability without inventing an unresolved numeric weight.
- Persisted player-specific discovery state per world under `Data/RuntimeState/<world>/classic-hidden-discoveries.json`.
- Preserved Classic pickup/re-drop behavior: hidden state is removed on pickup, discovery is forgotten for that instance, and re-drop is visible.
- Added executable Bundle D Search audit and installer integration.

## LEFT
- Phase D6 Cleanup.

## VERIFIED
- Discovery is player specific and not global.
- Hidden ground items remain hidden after discovery while still on the ground.
- Pickup removes cleanup-hidden state; subsequent re-drop is visible.
- Hidden and visible stacks remain separate.
- Look/examine/item targeting use discovery-aware visibility.
- See Hidden identity is consumed from Bundle A.

## STRONGLY CORROBORATED
- `classic.search.perception_vs_concealment.strongly_correlated.0167` bounded percentile comparison.

## DECODED BUT NOT MAPPED
- Hidden currency discovery awaits Phase D6 cleanup-state ownership.
- Generated hidden room objects without stable runtime instance IDs remain quarantined.

## CONFLICTS
- None introduced.

## TRUE UNKNOWN
- Exact native primary-stat weighting when concealment difficulty is absent.

## Files Added
- `Apps/Source/Reforged.Server/RuntimeBundleDSearch.cs`
- `Librarium/Reports/bundle_d_revision_0005.md`

## Files Modified
- `Apps/Source/Reforged.Server/RuntimeCommandSystem.cs`
- `Apps/Source/Reforged.Server/RuntimeClassicHiddenSearch.cs`
- `Apps/Source/Reforged.Server/RuntimeLiveWorld.cs`
- `Apps/Source/Reforged.Server/Program.cs`
- `INSTALL_AND_VALIDATE.bat`

## Files Removed
- None.

## Duplicate Paths Removed
- No source file was removed. The live visibility bypass in the generic ground-item loader was closed.

## Build Result
- Enforced by installer: win-x64 Release, warnings as errors, zero warnings and zero errors required.

## Audit Result
- Enforced by installer, including `--audit-bundle-d-search` and all cumulative Bundle D audits.

## Publish Result
- Enforced by installer to `Release/MajorMudReforged/Reforged.Server.exe`.
