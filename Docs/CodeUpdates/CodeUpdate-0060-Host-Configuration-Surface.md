# CodeUpdate-0060 - Host Configuration Surface

## Summary

CodeUpdate-0060 adds a read-only Server host configuration surface to bootstrap diagnostics.

The Server now reports safe host defaults and optional `Config/server.host.json` presence without starting gameplay, networking, or world simulation. Runtime toggles must remain disabled during this phase.

## Changed

- Added `ServerHostConfigurationAudit`.
- Added `ServerHostConfigurationReport`.
- Attached `host-configuration` to Server bootstrap.
- Printed host configuration status in Server startup output.
- Added smoke coverage that verifies runtime toggles remain disabled.

## Current Defaults

- Host name: `Reforged Local Host`.
- Bind address: `127.0.0.1`.
- Port: `4000`.
- Max sessions: `64`.
- World tick milliseconds: `1000`.
- Combat tick milliseconds: `6000`.
- Gameplay enabled: false.
- Networking enabled: false.
- World simulation enabled: false.

## Boundaries

- No gameplay system.
- No networking expansion.
- No world simulation loop.
- No runtime rule execution.
- No mode/profile activation.
- No combat.
- No character creation.
- No live editor behavior.
- No canonical `Data/` rewrite.
- No imported/reference deletion.
- No runtime formula or timing promotion.

## Verification

- ProjectManifest and UpdateManifest JSON parse passed.
- `git diff --check` passed.
- Sequential Release builds for Core, Server, Importer, Editor, Launcher, Client, and Core.Tests passed with 0 warnings and 0 errors.
- Core/server smoke tests passed.
- Server bootstrap passed and reported CodeUpdate 0060, validation 0 issues, `host-configuration`, `configured-runtime-disabled`, host `Reforged Local Host`, bind `127.0.0.1`, port `4000`, max sessions `64`, world tick `1000ms`, combat tick `6000ms`, gameplay disabled, networking disabled, world simulation disabled, and config file absent.
