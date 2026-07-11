# Reforged Roadmap

This is the compact roadmap for the current repository. Historical update chains should not be recreated here.

## Current Milestone

- Current Code Update: `0137`
- Current Database Version: `0010`
- Current Milestone: `CodeUpdate0137 - Complete Classic Gameplay`
- Status: complete locally, awaiting pushed GitHub Actions confirmation
- Verification: local complete runtime gate PASS 76/76 at `Reports/RuntimeVerification/complete_runtime_verification_20260711T143632Z.md`

## CodeUpdate0137 Completed Scope

- Character Foundation generation and runtime creation smoke.
- Race/class generation and validation for 195/195 combinations.
- Newhaven start at `room.2147`.
- Character save/reload smoke.
- Item/equipment generation and runtime smoke.
- Classic bug-fix contract enforcement, including mask face-slot and Lyrist's Companion repair.
- Generated data, room/exit, NPC, spell, spell-execution, combat, actors/abilities, interaction/narrative, services/economy, world/multiplayer, and scripted local-session smokes.
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

- `0138`: Multiplayer/server expansion after CodeUpdate0137 is pushed and GitHub Actions is green.
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
