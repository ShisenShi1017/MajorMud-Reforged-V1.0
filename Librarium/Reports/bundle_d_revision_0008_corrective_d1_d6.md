# Bundle D Revision 0008 — Combined Corrective Exhaustion D1–D6

## DONE

- Rechecked D1 through D6 together against generated Classic data, decoded MMExplorer room/exit datasets, semantic exit promotions, imported WCC relationships, Librarium catalogs, native/disassembly evidence, and live server consumers.
- Added one executable combined corrective audit: `--audit-bundle-d-corrective`.
- Fixed the Bundle D door audit command so it uses the resolved repository root and writes an audit artifact.
- Corrected the D1 field inventory recursion so nested evidence, relationships, metadata, prerequisites, effects, state semantics, validation, and provenance are inventoried instead of only `records`/`data` wrappers.
- Removed the unsupported use of ability 57 See Hidden as automatic permanent discovery of hidden room items, exits, NPCs, and objects. Ability 57 remains owned by the verified character/NPC concealment path.
- Clarified door authority: `RuntimeLivingWorldService` is the only live door command, movement, state, and persistence owner. `RuntimeDoorAccessService` remains non-live validation evidence and cannot independently mutate live world state.
- Preserved the verified cleanup mapping: decoded `Del@Maint` ability 119 maps to the runtime semantic property `DeleteAtCleanup` and native display meaning “Delete at cleanup.”

## LEFT

- Continue evidence promotion for decoded room fields that are present in source imports but absent from generated Classic records: light, shop, spell, lair, delay, fixed fields, and other imported metadata.
- Promote specialized exit consumers only where explicit evidence supports hidden mode, tolls, damage, action ordering, level/class/alignment restrictions, reciprocal state linkage, and automatic routing.
- Resolve hidden currency and non-item object identity/persistence before adding player-specific discovery for those target kinds.

## VERIFIED

- Canonical room identity is map-qualified. Newhaven Arena is `room.map1.2150`.
- `RuntimeWorld` owns the room and exit graph.
- Search discovery is player-specific and does not globally reveal the object.
- See Hidden does not automatically reveal or persist room-object discovery without direct source evidence.
- Ordinary room-ground items and currency become hidden at cleanup.
- Items carrying decoded `Del@Maint` ability 119 are deleted at cleanup.
- Live door traversal and door commands use one persisted runtime state owner.

## STRONGLY CORROBORATED

- The bounded percentile Search comparison remains active, but exact primary-stat weighting remains unresolved.
- Advanced door and exit semantics remain attached to decoded records and are activated only when explicit generated evidence supplies the requirement/effect.

## DECODED BUT NOT MAPPED

- Room light, shop, spell, lair, delay, and fixed imported fields not present in generated Classic runtime records.
- Exit hidden mode, toll amount, damage, required action sequence, advanced restrictions, and reciprocal state behavior without proven live consumers.
- Hidden currency and non-item hidden object target identities.

## CONFLICTS

- Historical reports used `del@cleanup` as though it were the decoded field name. The decoded field is `Del@Maint`; `DeleteAtCleanup` is the runtime behavior name.
- The richer door policy service looked like a second authority. It is now explicitly classified as non-live validation evidence; live ownership remains in `RuntimeLivingWorldService`.

## TRUE UNKNOWN

- Exact native Search stat weighting and target-specific concealment derivation.
- Universal maintenance/cleanup cadence and exact ordering relative to every reset cohort.
- Remaining native room latch and unclassified exit qualifier effects.

## Files Added

- `Apps/Source/Reforged.Server/RuntimeBundleDCorrectiveExhaustionAudit.cs`
- `Librarium/Reports/bundle_d_revision_0008_corrective_d1_d6.md`

## Files Modified

- `Apps/Source/Reforged.Server/Program.cs`
- `Apps/Source/Reforged.Server/RuntimeBundleDWorldAuthorityAudit.cs`
- `Apps/Source/Reforged.Server/RuntimeBundleDSearch.cs`
- `Apps/Source/Reforged.Server/RuntimeBundleDDoorAudit.cs`
- `INSTALL_AND_VALIDATE.bat`

## Files Removed

None.

## Duplicate Paths Removed

No source files were removed. The combined audit verifies that the richer door model is not registered as a parallel live execution path.

## Build Result

Not run locally because the creation environment does not contain the .NET SDK. The installer performs `dotnet restore`, warning-as-error win-x64 build, cumulative audits, and publish.

## Audit Result

Static corrective checks passed in the creation environment. The Windows installer runs the new combined corrective audit plus all cumulative Bundle A–D audits.

## Publish Result

Performed by `INSTALL_AND_VALIDATE.bat` to `Release\MajorMudReforged`, followed by verification of `Reforged.Server.exe`.
