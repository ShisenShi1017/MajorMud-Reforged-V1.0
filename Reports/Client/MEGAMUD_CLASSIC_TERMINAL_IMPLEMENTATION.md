# MegaMUD Classic Terminal Implementation

## Purpose
Make the Reforged gameplay terminal immediately familiar to MegaMUD players while retaining the modern Reforged Client systems already present in the application.

## Implemented presentation
- File, Edit, View, Action, Options, and Help menus.
- Compact toolbar whose center buttons control character auto features.
- Conversations selector for Main, Combat, Chat, Tells, Party, and System output.
- Large black text-first terminal.
- Classic bottom command entry.
- Blue session/status bar.
- Optional right-side Reforged tools for map/route, inventory/equipment, and character/session information.

## Modern additions
- Output-channel tabs synchronized with the Conversations selector.
- Hideable Conversations and Map/Tools panels.
- Command-history navigation with Up/Down.
- One-click quick commands and automation-area toggles.
- Existing six-session, mapping, routes, automation, coordination, parsing profiles, and Librarium functionality remains connected through the existing PlayViewModel and services.

## Boundaries
This update changes Client presentation and Client command wiring only. It does not modify Host gameplay, world state, characters, Librarium authority, Editor data, or generated Classic data.

## Visual fidelity limitation
The layout and workflow closely follow the supplied MegaMUD screenshots. Exact legacy bitmap icons and proprietary visual assets were not copied; compact text/symbol buttons are used until approved replacement artwork is created.
