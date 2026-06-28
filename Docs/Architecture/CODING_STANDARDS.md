# Coding Standards

## General

- Prefer simple, dependency-free Core code.
- Keep shared models in Core.
- Avoid duplicate models across applications.
- Keep application-specific logic out of Core.
- Favor readable code over clever code.

## Data

- Preserve unknown fields during import and serialization.
- Prefer human-editable JSON in `Data/`.
- Keep original imported artifacts in `ImportedDatabases/` as reference only.

## Runtime

- Runtime must not require the .NET SDK.
- Do not use SDK commands as the user launch path.
- Launcher and BAT compatibility scripts should call runtime-ready artifacts from `Bin/`.

## Updates

- Never touch `Saves/` or `ImportedDatabases/` without explicit approval.
- Every update must be runnable from its extracted folder.
- Every update must auto-locate the project root.
