# Code Update 0274 — Borin Server Cleanup Session Consolidation

## Purpose

Make server cleanup/restart a durable session boundary for Borin's external MajorMUD player without granting Git or repository-write authority.

## Runtime behavior

- Detects explicit server/system cleanup, restart, reboot, shutdown, maintenance, and reset warnings.
- Player speech, telepaths, and group chat cannot trigger the cleanup lifecycle.
- On the first cleanup warning/start signal Borin immediately stops autonomous gameplay actions and records a durable checkpoint before deeper work begins.
- The checkpoint preserves the active assignment, whether autonomous play should resume, verified room/vitals/level/XP, last command, server/world/character scope, and cleanup trigger.
- Borin runs a cleanup-boundary reflection when reflection is enabled, using the existing reflection journal rather than a second memory system.
- Verified gameplay learning is consolidated into the checkpoint/session summary. Model reflection remains explicitly separate from verified gameplay observations.
- Old raw `events-*.jsonl` gameplay journals are pruned only after successful consolidation. The current journal is never pruned. Default raw retention is seven days.
- If the cleanup disconnects the session, Borin retries the existing canonical Play session rather than creating another session.
- Reconnect defaults to every 20 seconds with no attempt limit. Borin keeps retrying until the existing session reaches a verified MajorMUD login/prompt, or the application/session is deliberately stopped. The durable checkpoint remains intact while the server is unavailable.
- After login returns, Borin sends a real `LOOK` perception refresh. He resumes the saved autonomous assignment only after fresh prompt/room/exits evidence is verified.
- If the application restarts while a cleanup checkpoint is pending, the lifecycle store reloads it and continues the reconnect/resume process.
- No Git commit, pull, push, repository creation, or remote-memory sync is performed in Code Update 0274. Those remain reserved for the later private-memory repository update.

## New configuration

`AI/Config/borin-player.json`:

- `cleanupLifecycleEnabled: true`
- `cleanupReconnectEnabled: true`
- `cleanupReconnectDelaySeconds: 20`
- `rawGameplayJournalRetentionDays: 7`

The existing general `autoReconnect` setting remains unchanged. Cleanup recovery is deliberately scoped to a checkpointed cleanup/restart event.

## Durable lifecycle storage

At runtime Borin writes a small bounded lifecycle ledger under:

`AI/Memory/Gameplay/<session-id>/borin-session-lifecycle.json`

It contains high-level verified state, selected verified gameplay lessons, the cleanup reflection text, resume goal, and lifecycle timestamps. It does not contain passwords, credentials, hidden model reasoning, or copied raw terminal transcripts.

## Deterministic test coverage

The Borin character harness now checks that:

1. Server cleanup warnings, active shutdown, and completion messages are recognized.
2. Player speech cannot spoof a cleanup event.
3. Ordinary non-server use of the word "cleanup" does not trigger the lifecycle.
4. The autonomous assignment is checkpointed before consolidation.
5. Verified gameplay count/lessons and reflection survive the durable checkpoint.
6. A pending checkpoint survives store/application reload.
7. Only expired raw journal files are pruned; the current journal remains.
8. Cleanup reconnect is intentionally unbounded and remains active until verified login succeeds or cancellation is requested.
9. A successfully resumed checkpoint moves out of the active slot.

## Validation note

The update was structurally audited in the packaging environment. A .NET SDK is not available in that environment, so the Release build and Borin character harness still need to be executed on a machine with the repository's .NET 8 toolchain before incorporation.
