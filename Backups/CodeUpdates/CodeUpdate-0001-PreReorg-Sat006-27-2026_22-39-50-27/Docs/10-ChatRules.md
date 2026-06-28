# Chat and Development Rules

These rules must be followed in future MajorMud Reforged chats.

## Source of truth

- The current GitHub repository or uploaded project ZIP is the code source of truth.
- User-provided project summaries are the architecture/design source of truth when the original PDF is unavailable.
- Imported MajorMud files are reference/import sources only.
- Reforged `Data/` is the standalone working database.

## Update rules

- Code updates are called CodePatches.
- Data/import releases are called Database Versions.
- Every update must run from its own extracted folder.
- Every update must include an Apply-*.bat until a compiled update manager replaces it.
- Every update must update Docs/.
- Every update must replace/update the root README.txt with what has been done.
- Never overwrite Saves/.
- Never overwrite ImportedDatabases/.
- Never claim an executable exists unless it has actually been created.

## Platform direction

- PowerShell is legacy/prototype infrastructure only.
- The long-term project should become standalone compiled applications.
- The user should not need to install the .NET SDK, Visual Studio, or developer tooling to run official releases.
- Executables must live in their own folders under Bin/Windows/<AppName>/.
