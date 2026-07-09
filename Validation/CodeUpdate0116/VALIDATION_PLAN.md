# CodeUpdate 0116 Validation Plan

1. Apply the update-only patch to the verified local repository.
2. Confirm `Saves/` and `ImportedDatabases/` still exist and were not overwritten.
3. Run `dotnet restore` for `Reforged.Server`.
4. Run `dotnet build` for `Reforged.Server`.
5. Run the local console server.
6. Verify commands: `look`, `state`, `events`, `entities`, `occupants`, `slots`, `npc`, `combat`, `cast`.
7. Confirm `cast` rejects execution and does not apply spell effects.
8. Confirm scheduler emits foundation events only and does not mutate gameplay state.
9. Commit and push only after build and smoke test pass.
