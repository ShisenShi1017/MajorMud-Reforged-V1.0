# Coding and Update Standards

## Repository Rules

- Keep the active repository streamlined.
- Do not reintroduce prototype folders at the root.
- Keep executable output in its own folder under `Bin/Windows/<AppName>/`.
- Source/application work belongs under `Apps/` and shared code belongs under `Core/`.
- `Data/` is the server's editable database source.
- `ImportedDatabases/` is reference/source material only.

## Update Rules

- Code Updates and Database Versions must run from their own extracted folder.
- Each update must include an `Apply-*.bat` until the compiled launcher/updater replaces it.
- The apply script must auto-locate the project root.
- Each update must update `README.md`, `CHANGELOG.md`, `VERSION.md`, and relevant `Docs/` files.
- Each update must preserve `Saves/` and `ImportedDatabases/` unless explicitly approved.

## Naming

- Database Version: game data/import/database conversion work.
- Code Update: code, repository, tooling, launcher, server, editor, and client work.
