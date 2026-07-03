# Reforged Development Principles

This document describes how Reforged should be engineered. `AGENTS.md` describes handoff and assistant workflow rules; this file describes project engineering principles.

## Core principles

1. GitHub is the single source of truth.
2. Never guess; verify before claiming success.
3. Prefer small, reversible, verifiable Code Updates.
4. GitHub Actions is the official build gate after a push.
5. Keep Milestone 1 Core frozen unless an explicit fix is required.
6. Applications depend on Core; Core must not depend on applications.
7. Shared contracts and shared models belong in Core.
8. Runtime users should not need the .NET SDK.
9. Preserve `Saves/` and `ImportedDatabases/` unless the user explicitly approves otherwise.
10. Documentation must change with the code so handoffs stay accurate.
11. Treat all WCC data as reference-only unless the user explicitly approves a reviewed import or reconciliation update.
12. Treat game logic as data-driven by default; keep rules, content, progression, encounters, shops, quests, spawns, textblock actions, and world behavior in data unless a behavior is truly engine-internal.

## Server development principles

- Build the server in narrow vertical slices.
- Start with bootstrap, configuration, diagnostics, and data validation before gameplay.
- Do not add combat, character creation, world simulation, networking expansion, or live editor behavior without explicit approval.
- Every server slice should add smoke-test coverage or explain why it cannot.
- Startup should fail clearly when repository identity, configuration, data, or required folders are invalid.
- Server code should provide reusable engine systems, validation, execution, and enforcement rather than hardcoded game-specific behavior.

## Update discipline

- One approved scope per Code Update.
- No broad rewrites unless the user explicitly approves them.
- No hidden feature additions.
- Record exactly what changed, what was verified, and what remains pending.
- Keep `AGENTS.md`, `AI_CONTEXT.md`, README, CHANGELOG, VERSION, ProjectManifest, ReleaseNotes, roadmap, and affected app docs synchronized.

## Quality preference

Favor clear, maintainable, boring code over clever code. Reforged is a long-term software project, so future readability and safe handoff are more important than speed.
