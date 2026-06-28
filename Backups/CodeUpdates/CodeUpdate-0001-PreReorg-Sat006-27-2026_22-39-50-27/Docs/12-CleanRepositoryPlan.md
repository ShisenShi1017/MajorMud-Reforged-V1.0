# Clean Repository Plan

## Goal

Make MajorMud Reforged easy to navigate, easy to upload to GitHub, and ready for standalone app development.

## New top-level layout

```text
Apps/                 User-facing app folders and app documentation
Bin/Windows/          Future standalone executable folders
Data/                 Standalone editable Reforged database
Docs/                 Project documentation and handoff history
ImportedDatabases/    Original reference/import sources only
Legacy/               Older prototype files moved here instead of deleted
Logs/                 Runtime logs and validation reports
Saves/                Player saves; must not be overwritten
Source/               Clean source layout
Tools/                Import, validation, update, and developer tools
```

## Legacy handling

Old prototype files are moved into `Legacy/PowerShellPrototype/` rather than deleted. This allows the repository to be uploaded without manually removing files while still keeping the root clean.

## Executable handling

Future executables must be placed in their own folders:

```text
Bin/Windows/Launcher/
Bin/Windows/Server/
Bin/Windows/Editor/
Bin/Windows/Importer/
Bin/Windows/Client/
```

## No SDK requirement for user

The user should not need to install .NET SDKs or developer tools to run official builds. If source code requires a build system, released binaries should be supplied separately in the correct `Bin/Windows/<AppName>/` folder.
