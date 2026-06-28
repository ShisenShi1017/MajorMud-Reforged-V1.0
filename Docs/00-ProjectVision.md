# MajorMud Reforged Project Vision

Last Updated: 2026-06-27

## Purpose

MajorMud Reforged is a professional, versioned effort to create a standalone MajorMud experience with a complete server, editor tools, and eventual modern clients inspired by MegaMud 2.0.

The original design PDF was not available, so this document becomes the living replacement for the design history and architectural vision. User-provided project summaries are authoritative when the original PDF is unavailable.

## Core Vision

MajorMud Reforged is being built in three major phases:

1. Standalone MajorMud server.
2. Host/world editor.
3. Future client work similar in spirit to MegaMud 2.0.

## Phase 1: Standalone Server

The standalone server is the highest priority.

Goals:

- Run independently without requiring an external BBS host.
- Preserve the MajorMud feel and gameplay flow.
- Support imported MajorMud database content where practical.
- Maintain stable save/load behavior.
- Avoid unnecessary rewrites.
- Preserve backward compatibility unless a migration is explicitly approved.

## Phase 2: Editor

The editor should eventually support safe editing of game content including, but not limited to:

- Areas.
- Rooms.
- Monsters.
- Items.
- Shops.
- NPCs.
- Quests.
- Spells.
- Classes.
- Races.
- Skills.
- Messages.
- Global settings.
- Import/export tools.

The editor should respect imported databases and save files. It must not overwrite user-created saves or imported database material without explicit approval.

## Phase 3: Client Work

Future clients should be inspired by MegaMud 2.0 while remaining compatible with the standalone MajorMud Reforged server.

Potential features:

- Mapping.
- Triggers.
- Aliases.
- Macros.
- Scripting.
- Character profiles.
- Chat tabs.
- Multiple panes/windows.
- Hotkeys.
- Themes.
- Windows client.
- Android client.

## Project Philosophy

The game comes first.

Gameplay accuracy, data safety, and project continuity matter more than flashy features. Every change should be small, explainable, and consistent with the existing project structure.

## Documentation Rule

Because the original PDF was not available, this document and the companion files in `Docs/` are the project memory. Every future patch should update the relevant documentation so new development sessions can continue without losing context.
