# Code Update 0280 — Borin Self-Play Validation and Safety Hardening

## Purpose

Audit the cumulative autonomous-player stack from Code Updates 0271–0279 and harden proven edge cases without taking ordinary MajorMUD decisions away from Borin.

## Hardened autonomous command boundary

A deterministic autonomous-only safety policy now runs after the normal terminal gateway and before Borin sends a self-directed command.

It blocks:

- account/session exit commands such as QUIT, LOGOFF, LOGOUT, DISCONNECT, and HANGUP;
- character-destruction commands such as DELETE, ERASE, REROLL, and SUICIDE;
- SYSOP/GAMEOP/admin/wizard/GOTO aliases and other privilege-control roots;
- script, macro, shell, credential, and operating-system control roots;
- command separators/batching (`;`, `&&`, `||`) so one AI decision can produce only one terminal action;
- underspecified state-changing commands that require a concrete target/argument;
- SELL/DROP/GIVE/PUT/WEAR/EQUIP/DRINK/EAT when Borin does not have a fresh live inventory capture;
- REMOVE when Borin does not have a fresh live equipment capture.

This policy does not block normal autonomous combat, spells, travel, shops, quests, NPC interaction, communication, leveling, equipment decisions, or survival actions. The external MajorMUD server remains authoritative.

## Stale-perception deadlock recovery

The cumulative self-play loop previously treated a stale prompt/room as a wait condition. If the server was still connected but no new prompt would arrive until another command, Borin could wait forever.

0280 distinguishes temporary movement from stale perception:

- while a movement command is still resolving, Borin waits for the server;
- when the session is connected/logged in but room or prompt evidence is stale/missing, Borin sends one safe LOOK perception refresh;
- the next autonomous action is not selected until fresh server evidence is evaluated;
- failed refreshes remain evidence-gated and do not authorize command flooding.

## Cleanup-memory sync timing

Local cleanup checkpointing/reflection/consolidation can still begin on the first trusted cleanup warning, preserving the safe pre-cleanup snapshot.

The private `Borin-Memory` Git push no longer occurs merely because a warning was seen. Remote synchronization is deferred until a confirmed post-cleanup boundary:

1. the cleanup disconnect actually occurs, or
2. the server explicitly reports cleanup completion.

This matches the intended lifecycle: preserve locally before cleanup, push durable consolidated memory after the cleanup boundary. Git failure remains non-blocking for reconnect and gameplay.

## Validation coverage added

The Borin character test harness now verifies that:

1. autonomous QUIT/session exit is rejected;
2. SYS/GOTO privilege-control aliases are rejected;
3. a single model action cannot batch multiple commands;
4. ordinary combat remains permitted;
5. irreversible inventory actions require fresh live inventory evidence;
6. equipment removal requires fresh live equipment evidence;
7. a cleanup warning/start is not treated as the post-cleanup Git-push boundary;
8. explicit cleanup completion is recognized as a confirmed post-cleanup memory boundary.

## Packaging rule

This cumulative ZIP retains every file created or modified by Code Updates 0271–0280 exactly once, at its newest version. Unrelated untouched repository files, runtime `AI/Memory` contents, JSONL journals, binaries, and build output are excluded.

## Validation note

The packaging environment can perform structural/static audits and the repository's Python validator, but it does not provide the repository's .NET SDK toolchain. Run the Release build and Borin character harness on the development PC before incorporating the cumulative update.
