# Reforged Host Vision V1

## Multi-World Host

The Host is designed to run multiple worlds at the same time under one Reforged Realms host identity.

Example target shape:

- Classic World
- Reforged Custom World A
- Reforged Custom World B
- Pure AI World
- Test Realm A
- Test Realm B
- Future modules such as T-Lord

Each world owns isolated characters, world data, configuration, rules, AI settings, logs, snapshots, branding, login screen, and MOTD.

## Host Authority

The Host is always authoritative. It owns world state, character state, combat, items, NPCs, quests, economy, spawn timers, resets, permissions, and publishing. Clients request actions; the Host decides and mutates state.

## World Types

Classic worlds are protected. They preserve original gameplay, block imports/exports and character transfers, prioritize formula fidelity, and allow editable content only inside original logic boundaries.

Reforged Custom worlds are fully editable. They may support new items, races, classes, spells, mechanics, EXP curves, stats, formulas, events, commands, and other custom content.

Pure AI worlds are AI-managed. AI may control events, loot, NPC behavior, quests, dynamic encounters, and economy where explicitly enabled.

## AI Layer

AI is both a possible world type and a per-world layer. Custom worlds may configure AI as Off, Assistive, Managed, or Director. Classic remains AI Off unless explicitly approved for a separate non-contaminating experiment.

## Test Realms And Publishing

Editor changes go to a test realm first, not directly to live. The intended flow is Editor to Test Realm to Validation to Publish to Live World.

Publishing may be immediate, next reset, scheduled, or selected changes only. Before publishing, the Host should create a snapshot, diff, audit log, and rollback package.

## Import Wizard

The import wizard may ingest existing Worldgroup server material such as WCC, world data, menus, text, and configuration. Imports create a Reforged Custom Test Realm and never overwrite a live world.

## World Identity

Every world should support name, description, branding, logo, banner, login screen, MOTD, changelog, and online player display.

## Cross-World Features

Optional Host-level features may include cross-world chat, shared announcements, and host broadcasts. Character state remains isolated according to transfer rules.

Classic blocks transfers. Custom worlds may optionally allow transfers. Pure AI worlds may receive character copies. Nothing contaminates Classic.

## Reset And Live Editing

The Host owns world reset behavior, including standard cleanup, configurable timers, persistent items, hidden items, deleted items, hard-reset NPCs, complete world wipe, and optional character wipe.

Live editing is mediated by test realms and publishing. The Editor prepares changes; only the Host publishes them.

## Permissions

The Host controls roles and capabilities for SysOps, builders, GMs, moderators, and players.

## Networking And Scripting

The Host serves multiple clients, worlds, and sessions. Clients request; the Host decides.

Client-side scripting may exist, but the Host determines what automation is allowed.

## Future Professional Host Features

Cluster support should remain a future architecture option so multiple Host instances may cooperate as one visible server.

Maintenance mode should allow the Host to block new logins, let current players finish, broadcast countdowns, perform backups, publish updates, and restart safely.

A world health dashboard should eventually show online/offline state, player count, CPU and memory usage, AI activity, next reset, backup status, test realm status, active events, and recent errors or warnings.

## CodeUpdate 0115 Position

CodeUpdate 0115 starts the backbone for this vision by adding host-owned runtime state and a generic runtime event dispatcher. It does not implement combat, magic, NPC AI, spawn logic, quests, dialogue, scripts, guilds, economy integration, clusters, maintenance mode, or dashboards.
