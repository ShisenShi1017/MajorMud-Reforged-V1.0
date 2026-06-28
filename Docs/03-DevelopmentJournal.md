# Development Journal

## Code Update 0001 — Repository Reorganization & Core Foundation

### Completed

- Cleaned the repository into the approved long-term layout.
- Added `Apps/` app-family folders.
- Added `Core/` shared foundation folder.
- Added `Bin/Windows/` executable destination folders.
- Added `Updates/Code/` and `Updates/DatabaseVersions/`.
- Added root `README.md`, `CHANGELOG.md`, and `VERSION.md`.
- Added documentation for repository reorganization and Core architecture.
- Preserved obsolete/prototype material under `Legacy/`.

### Design decision

The project is moving away from PowerShell as an implementation platform. PowerShell prototype material may remain archived for reference, but real server/editor/importer/launcher/client work should move toward compiled applications with each `.exe` in its own folder.

### Next work

Build the Core data loader and server bootstrap using the new structure.


## CodeUpdate-0003 — Layout Freeze, Manifest, and Redundancy Cleanup

- Added `ProjectManifest.json`.
- Removed redundant duplicate README text files.
- Standardized executable folder policy.
- Froze the repository layout so Core and Server work can proceed.
