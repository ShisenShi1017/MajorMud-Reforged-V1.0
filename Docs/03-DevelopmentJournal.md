# Development Journal

## CodePatch 0002 - Clean Repository / App Platform Foundation

### Completed

- Added clean `Apps/` layout.
- Added `Bin/Windows/<AppName>/` folders for future executables.
- Added clean `Source/` layout for Core, Server, Editor, Importer, Launcher, and Client.
- Added app platform documentation.
- Added clean repository plan.
- Updated chat/development rules.
- Updated root README.txt.
- Added compatibility apply script that runs from its own extracted folder.
- Added migration behavior that moves old PowerShell prototype files into `Legacy/PowerShellPrototype/` instead of deleting them.

### Not completed

- No compiled executable is included in this update.
- No server gameplay is implemented in this update.
- No save/database migration is performed.

### Next recommended step

Create the first real app foundation component without requiring the user to install developer SDKs. The likely next target is a prebuilt/importer-free server loader design or launcher shell once a build path is available.
