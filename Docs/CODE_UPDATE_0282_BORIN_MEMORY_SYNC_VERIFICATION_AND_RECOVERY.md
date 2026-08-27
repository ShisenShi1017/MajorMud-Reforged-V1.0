# Code Update 0282 — Borin Memory Sync Verification and Recovery

## Goal

Make the private `Borin-Memory` repository a reliable durable-memory destination even when GitHub, networking, Git credentials, or the application fail at the cleanup boundary.

## Changes

- Added a local-only durable memory-sync outbox under `AI/Memory/.borin-memory-sync-outbox.json`.
- A confirmed cleanup snapshot is staged into the outbox before the lifecycle checkpoint can be moved to history.
- Failed Git commits/pushes do not discard the cleanup snapshot.
- Pending cleanup snapshots survive application restart and are deduplicated by cleanup ID.
- Borin retries pending private-memory pushes until they succeed. There is no attempt limit.
- Retry delay defaults to 300 seconds and is configurable with `memoryRepositoryRetryDelaySeconds` (30–3600 seconds).
- Retry behavior can be disabled independently with `memoryRepositoryRetryEnabled` without disabling the rest of Borin's memory system.
- Multiple offline cleanup snapshots can be recovered in one later Git commit while retaining each compact session summary.
- A push that actually reached GitHub but crashed before local acknowledgement is idempotent: the outbox is cleared without creating a duplicate commit when Git reports no changes.
- Borin's reconnect/gameplay path never waits for GitHub. Memory sync failure remains a background durability concern, not a gameplay blocker.
- The outbox itself is operational state and is explicitly excluded from the private repository allowlist.

## Validation

`Tools\Borin\run_memory_sync_validation.ps1` performs a non-interactive Git preflight on the configured private repository:

1. verifies Git is installed;
2. verifies the configured `main` branch is readable with saved authentication;
3. clones the private repository into a temporary sandbox;
4. performs `git push --dry-run` to verify push permission without modifying GitHub;
5. verifies the initialized README exists;
6. runs the Borin character/self-play harness unless `-SkipCharacterTests` is supplied.

The Borin character harness also verifies that the local outbox survives reload, deduplicates cleanup IDs, removes only acknowledged snapshots, and is not eligible for upload as durable memory.

## Runtime rule

Normal sequence after server cleanup:

`consolidate locally → confirmed cleanup boundary → stage durable outbox → try Git push → reconnect/play regardless → retry in background until Git succeeds → clear acknowledged outbox`

The private repository remains limited to bounded durable Borin memory and compact cleanup summaries. Credentials, raw terminal journals, source code, binaries, generated Librarium indexes, and the operational outbox are not synchronized.
