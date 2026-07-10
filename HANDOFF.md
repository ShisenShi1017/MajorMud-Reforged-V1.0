# MajorMUD Reforged Handoff — Post CodeUpdate0133

## Active result
CodeUpdate0133 integrates generated room/exit loading with a session-controlled developer avatar and formalizes Omniverse as an evidence source.

## Runtime actor model
A `RuntimeSession` controls an `IRuntimeSessionActor`:
- `RuntimeCharacterActor`: a real gameplay character.
- `RuntimeDeveloperAvatar`: temporary session infrastructure used for runtime smoke testing.

The developer avatar has no class, race, level, inventory, currency, character-creation bypass, or persistence record. It is not a Librarium or generated-data entity.

## Session order
1. Restore a valid real character and room when available.
2. Otherwise attach the developer avatar when developer mode permits.
3. Use an explicitly configured diagnostic generated room when available.
4. Otherwise use the isolated synthetic Developer Test Room.
5. With developer mode disabled and no valid character, stop with a diagnostic because normal character creation is not yet implemented.

## Remaining Classic spawn gap
Generated Classic data does not yet expose a verified new-character starting location. Town Gates and legacy generic `start_room` metadata must not substitute for that evidence.

## Omniverse evidence policy
`Evidence/Omniverse` now preserves Omniverse Discord, website, document, video, and message-export material. Classic research is separated from intentional OmniMUD features. Nothing is canonical until validated and promoted into the Librarium with provenance.

## Evidence included
- Combat/spell technical discussion.
- Aggro target-memory and daily-maintenance discussion.
- Dragon-hide legacy mapping investigation and adjustment discussion.
- MegaMUD/Omniverse connection configuration reference.

## Next actions
1. Run the C# build and runtime smoke commands in `PATCH_INSTRUCTIONS.md`.
2. Do not promote Omniverse formulas or mappings directly.
3. Begin OVGN validation passes against DLL evidence and ImportedDatabases.
4. Do not begin CodeUpdate0134 NPC loading until 0133 runtime verification passes.
