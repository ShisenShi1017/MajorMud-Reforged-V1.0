# MajorMud Reforged Update System

## Code Updates

Code Updates modify project code, docs, scripts, manifests, and architecture. They are numbered `CodeUpdate-XXXX`.

## Database Versions

Database Versions modify converted/imported game data. Database work must be called a Database Version, not a patch.

## Required Package Contents

Every Code Update must include:

- `Apply-CodeUpdate-XXXX-Name.bat`
- `UpdateManifest.json`
- `Payload/` containing changed files
- documentation/report files as appropriate

## Execution Rule

The apply BAT must run from the extracted update folder and apply to the main project folder. It must automatically locate the project root by checking for `ProjectManifest.json`, `README.md`, `Core/`, and `Data/`.

## Preservation Rule

Update packages and scripts must not overwrite:

- `Saves/`
- `ImportedDatabases/`

unless the user explicitly approves it.

## Runtime Rule

No Code Update may make normal runtime depend on the .NET SDK. Runtime must use compiled/runtime-ready artifacts in `Bin/`.

## Verification Rule

Never announce a package until the ZIP exists and has been verified.

## CodeUpdate-0010 Handoff and Update Folder Rule

Added in CodeUpdate-0010:

- `AI_CONTEXT.md` is a living handoff document and must be updated with every Code Update.
- The handoff must include the current milestone, update number, completed/substantially-underway table, Milestone 1 completion checklist, remaining work, and next recommended work.
- Every Code Update and Database Version ZIP must contain exactly one labeled top-level folder named for the update.
- The user must be able to extract the ZIP directly into the main project folder without manually creating a folder.
- The `Apply-*.bat` file must live inside that labeled update folder and run from there.
- The apply script must automatically locate the project root before copying payload files.
- Approved suggestions are implementation requirements unless explicitly deferred or rejected.

