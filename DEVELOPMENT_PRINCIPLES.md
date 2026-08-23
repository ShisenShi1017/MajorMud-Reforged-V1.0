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
13. Every structurally decoded MajorMUD gameplay field must have a verified Librarium-to-generated-Data-to-runtime-consumer path. Required decoded data may fail closed when exact behavior is unproven, but it must not be silently dropped.
14. Use decoded Worldgroup behavior only when MajorMUD depends on it for gameplay, module/session dispatch, scheduling, persistence, recovery, or client protocol compatibility. Do not reproduce unrelated BBS menus, boards, mail, chat, accounting, modem/dial-up, door-management, or host-administration behavior.
15. Classic has no generated AI-profile or guild subsystem. App AI is tooling outside gameplay authority; NPC behavior comes from decoded NPC, spawn, ability, and world data. Candidate `ai_profiles` and `guilds` Librarium evidence must not be emitted as Classic runtime families.
16. Candidate `events` records marked `pending_generator_export` are Librarium evidence, not a parallel runtime authority. Spawn/monster timing is generated and consumed from canonical room, spawn, and NPC data; live events are produced by the authoritative runtime event and scheduling services.
17. Catalog summaries, evidence bookkeeping, editor/client views, conflicts, validation policy, and Custom/AI overlay definitions remain in Librarium tooling. They are not Classic runtime families unless they contain concrete MajorMUD gameplay records with a verified live consumer.

## Server development principles

- Build the server in narrow vertical slices.
- Start with bootstrap, configuration, diagnostics, and data validation before gameplay.
- Do not add combat, character creation, world simulation, networking expansion, or live editor behavior without explicit approval.
- Every server slice should add smoke-test coverage or explain why it cannot.
- Startup should fail clearly when repository identity, configuration, data, or required folders are invalid.
- Server code should provide reusable engine systems, validation, execution, and enforcement rather than hardcoded game-specific behavior.
- Runtime validation must distinguish MajorMUD-required Worldgroup dependencies from BBS-only evidence. BBS-only evidence may remain as provenance, but it must not become a generated runtime dependency.

## Update discipline

- One approved scope per Code Update.
- No broad rewrites unless the user explicitly approves them.
- No hidden feature additions.
- Record exactly what changed, what was verified, and what remains pending.
- Keep `AGENTS.md`, `AI_CONTEXT.md`, README, CHANGELOG, VERSION, ProjectManifest, ReleaseNotes, roadmap, and affected app docs synchronized.

## Quality preference

Favor clear, maintainable, boring code over clever code. Reforged is a long-term software project, so future readability and safe handoff are more important than speed.
