# Reforged Roadmap

This is the compact roadmap for the current repository. Historical update chains should not be recreated here.

## Current Milestone

- Current Code Update: `0138`
- Current Database Version: `0010`
- Current Milestone: `CodeUpdate0138 - Server Runtime and Host Enablement`
- Status: closed locally, awaiting pushed GitHub Actions confirmation
- Verification: local complete runtime gate PASS 55/55 at `Reports/RuntimeVerification/complete_runtime_verification_20260712T173531Z.md`

## CodeUpdate0138 Completed Scope

- Server runtime and host enablement.
- Host configuration, module readiness, lifecycle, scheduler bootstrap, and scheduler status command audit surfaces.
- Runtime live gameplay, multiplayer, living world, combat, interaction/narrative, services/economy, and world-system smoke coverage.
- Generated Classic data refresh from Librarium.
- Map-qualified movement data promotion through Librarium.
- Textblock quest-flow fix 0142 promotion into Librarium quests.
- Active documentation cleanup into a compact root/docs surface.

## Architecture Direction

- Librarium is authoritative.
- `/Data` is generated runtime output.
- Runtime consumes generated `/Data`.
- Classic is immutable and evidence-locked.
- Custom and AI worlds use overlays.
- Client is read-only for gameplay state.
- Host/server is authoritative.
- Imported/reference material remains untouched unless the user explicitly approves cleanup.

## Server Direction

Build the server as a reusable multi-world host, not a Classic-only executable.

Classic is the first target because it is the verified baseline. The design must stay ready for:

- Classic world
- Reforged Custom worlds
- Pure AI worlds
- Test realms
- Future modules

## Planned Public Updates

- `0139`: MegaMUD-style telnet client, excluding BBS features.
- `0140`: Custom/AI overlay-world support.
- `0141`: Host editor.

Numbers may shift only if the user explicitly approves a renumbering.

## Documentation Policy

Keep active documentation compact. Current continuity belongs in:

- `Docs/PROJECT_STATUS.md`
- this roadmap
- root continuity docs and manifests

Do not restore large historical doc trees unless the user asks.
