# Runtime Execution Policy

MajorMud Reforged must not require the .NET SDK to run.

Rules:

- Running the project must use compiled executables or runtime-ready artifacts under `Bin/`.
- SDK-only launch/build commands are development/build-time activities only and must never be required by a normal user to launch the game, server, editor, importer, or launcher.
- Update packages must run from their own extracted folder and apply into the main project folder using the included `Apply-*.bat` script.
- Update packages must not delete or overwrite `Saves/` or `ImportedDatabases/` unless the user explicitly approves that action.
