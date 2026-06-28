# CodePatch 0002

Name: Clean Repository / App Platform Foundation
Type: CodePatch

## Purpose

Clean the repository structure and establish the long-term standalone app platform layout.

## Apply behavior

- Must be run from the extracted update folder.
- Automatically locates the project root.
- Copies new app/source/docs/tool layout into the project.
- Moves legacy PowerShell prototype files into `Legacy/PowerShellPrototype/`.
- Does not delete protected data.

## Protected folders

- Saves
- ImportedDatabases
- Data
