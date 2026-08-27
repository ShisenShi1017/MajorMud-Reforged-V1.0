# Code Update 0275 — Borin Private Memory Repository

## Purpose

Give Borin a separate private Git repository for durable memory without placing Reforged source, credentials, raw terminal logs, generated indexes, backups, or model files in that repository.

This update builds on Code Updates 0271–0274. The server-cleanup boundary remains Borin's main consolidation event. Local cleanup reflection and deterministic gameplay-memory consolidation may begin as soon as a trusted cleanup warning is seen, but the private Git commit/push is deliberately deferred until the server actually disconnects for cleanup or explicitly reports cleanup completion. A Git failure never blocks cleanup recovery, reconnect, or autonomous play; the local memory remains authoritative and the next cleanup can try again.

## Configured private repository

The default player configuration now points to:

`https://github.com/ShisenShi1017/Borin-Memory.git`

Branch: `main`

The repository must be **private** and initialized with an initial commit (for example, a README) before the first sync. This update does not create a GitHub repository and does not store a GitHub token. Authentication must already work through normal Git for Windows credentials/Git Credential Manager or SSH.

The local Git mirror defaults to:

`%LOCALAPPDATA%\MajorMudReforged\BorinMemoryRepository`

The sync service rejects a mirror location inside the Reforged source repository so Borin's private memory Git metadata cannot contaminate a Reforged release/source commit.

## Durable-memory allowlist

The memory repository receives bounded state from Borin's real memory systems, including:

- core memory
- episodic memory
- social memory
- character growth
- initiative/natural initiative
- living-character state
- voice continuity
- temporal continuity
- situational awareness
- conversation repair/group conversation state
- memory consolidation state
- verified gameplay learning
- bounded reflection journals
- verified learned knowledge
- Academy/mastery progress
- compact server-cleanup session summaries

## Intentionally excluded

The sync is allowlist-based. It does not copy arbitrary `AI/Memory` contents.

Excluded data includes:

- raw `events-*.jsonl` terminal/gameplay journals
- live cleanup/reconnect checkpoint files
- `AI/Memory/Backups`
- temporary files
- generated/reconstructible knowledge indexes and Librarium catalogs
- model files
- Reforged source code
- build/test output
- Windows Credential Manager data
- passwords, API keys, GitHub tokens, or credential-bearing Git URLs

Cleanup session checkpoints stay local because restoring a stale remote checkpoint on another computer could incorrectly make Borin believe a server cleanup is still in progress. Instead, 0275 writes compact completed cleanup summaries into the private repository.

## Startup behavior

When Borin's player service is configured and memory sync is enabled:

1. Git verifies the dedicated private repository using the configured credential-free GitHub URL.
2. The private repository is pulled into the local mirror.
3. Remote durable memory restores missing local memory files.
4. A local file that changed since the last successful synchronization is never overwritten by startup pull.
5. Borin's reflection journal and cleanup lifecycle are then loaded normally.

If Git/GitHub is unavailable, Borin continues with his local memory.

## Cleanup behavior

After a trusted server cleanup/restart warning or start signal, Borin checkpoints and consolidates locally. The remote-memory push waits for a confirmed post-cleanup boundary (cleanup disconnect or explicit cleanup-completed signal):

1. Borin stops normal autonomous gameplay actions.
2. The current assignment is checkpointed locally.
3. Automatic cleanup reflection runs.
4. Verified gameplay learning is consolidated.
5. Expired raw gameplay journals are pruned under the existing retention rule.
6. Borin waits for the cleanup disconnect or explicit cleanup-completed evidence before remote sync.
7. After that confirmed boundary, 0275 refreshes the private Git mirror.
8. Only allowlisted durable memory is copied into the mirror.
9. A compact cleanup-session summary is written.
10. Git commits only when the durable snapshot changed.
11. Git pushes to the configured private repository.
12. A Git failure is logged as deferred; it does not prevent Borin from reconnecting and resuming.

Borin's existing cleanup reconnect loop still retries until he logs back in.

## Local disk-growth policy

This architecture does not use Git as a raw event database. High-volume raw terminal journals remain outside the private memory repository and are pruned locally by the existing retention policy. The Git repository stores bounded consolidated JSON state and compact cleanup summaries. Git runs `gc --auto` after successful pushes.

## Security boundary

0275 accepts only credential-free `github.com` HTTPS or normal GitHub SSH remote forms. Embedded HTTPS usernames/tokens, query strings, fragments, and non-GitHub remotes are rejected. Git authentication remains external to Borin's JSON configuration.

The private memory repository has no authority over the Reforged source repository. This update does not enable Borin engineering Repository Updates, source commits, or source pushes.

## Test coverage added

The deterministic Borin character harness now verifies that:

- core durable memory is allowlisted;
- reflection and verified-learning memory are allowlisted;
- raw JSONL journals are rejected;
- operational cleanup checkpoint files are rejected;
- backup and generated knowledge-index files are rejected;
- the configured credential-free GitHub remote is accepted;
- GitHub SSH is accepted; and
- credential-bearing HTTPS and non-GitHub remotes are rejected.

## Verification note

The cumulative package is source/config/documentation only. A .NET SDK is not available in the packaging environment, so the Windows Release build and Borin deterministic harness must still be run on the development PC before this update is merged into the main repository.
