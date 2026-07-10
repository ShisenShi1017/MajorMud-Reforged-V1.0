# CodeUpdate0133 Patch Instructions

## Baseline
Apply to the CodeUpdate0132 full repository. This archive is update-only.

## Apply
Copy the archive contents over the repository root while preserving paths.

## Build validation
From the repository root, run the solution/project build commands used by CodeUpdate0132. At minimum build the compiler and `Reforged.Server` with the installed .NET SDK.

## Runtime smoke validation
1. Confirm the active world loads generated `rooms.json` and standalone `exits.json`.
2. Start the server with developer mode enabled.
3. Confirm the session attaches `RuntimeDeveloperAvatar`, not a gameplay character.
4. Confirm `look` renders the diagnostic room.
5. When an approved generated diagnostic room is configured, verify visible exits and movement.
6. Confirm Town Gates is not used as a default spawn.
7. Confirm the developer avatar is not written to character state or persistence.

## Evidence validation
1. Confirm all files under `Evidence/Omniverse` are present.
2. Preserve screenshots unchanged.
3. Do not copy Omniverse conclusions into the Librarium without an OVGN verification record.
4. Keep stock/Classic findings separate from OmniMUD enhancements.

## Promotion gate
Do not promote CodeUpdate0133 until the C# build and runtime smoke validation pass. Evidence intake itself does not authorize gameplay-data changes.
