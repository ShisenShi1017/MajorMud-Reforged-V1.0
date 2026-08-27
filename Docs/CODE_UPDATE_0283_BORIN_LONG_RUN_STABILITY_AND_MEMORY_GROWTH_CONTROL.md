# Code Update 0283 — Borin Long-Run Stability and Memory Growth Control

## Objective

Keep Borin capable of continuous autonomous play for days or longer without allowing local journals, reflection/social state, cleanup summaries, operational recovery debris, or the dedicated private Git memory repository to grow without bound.

## Changes

### Reflection/social journal bounds

The private Borin player journal now enforces its retention limits both while writing and when an older journal is loaded:

- 500 recent conversation summaries
- 250 recent reflections
- 150 verified social lessons
- 200 recent speaker profiles

This prevents an older oversized ledger from remaining oversized forever after an upgrade.

### Verified gameplay learning bounds

Each world/character profile was already bounded by route, outcome, danger, recovery, item, milestone, and leveling-area limits. 0283 additionally caps inactive world/character profiles at 64, retaining the most recently observed profiles.

### Raw gameplay journal disk quota

Raw `events-*.jsonl` journals are still excluded from Borin-Memory and remain local-only. Cleanup maintenance now applies all three controls:

- age retention: 7 days by default
- total retained raw-journal storage: 64 MB by default
- retained raw-journal files: 48 by default

The active journal is never deleted by the cleanup retention pass. Locked files are skipped rather than turning maintenance into a gameplay failure.

### Operational outbox debris

If the local private-memory outbox becomes unreadable, Borin still quarantines it for diagnosis, but only the 3 newest corrupt outbox archives are retained.

### Bounded private-repository session summaries

The private `Borin-Memory` checkout retains 180 cleanup/session summaries by default. This limit is configurable and is enforced before each memory commit.

### Git history compaction

Deleting old session files from a Git working tree does not remove their bytes from old commits. Therefore 0283 also bounds repository history itself.

By default, after 50 successful memory snapshot commits Borin:

1. completes the normal memory commit and push;
2. writes a maintenance marker resetting the compaction counter;
3. creates a new root commit containing the complete current memory snapshot;
4. replaces the private memory branch with that snapshot using `--force-with-lease` against the exact commit Borin just pushed;
5. leaves the operation untouched if the lease no longer matches, so another writer cannot be silently overwritten.

The private repository is dedicated to Borin durable memory, so old Git history is not treated as the memory authority. The latest bounded snapshot is the authority. If compaction fails, the already-pushed memory remains valid and compaction is retried on a later cleanup.

### Duplicate-commit protection

The history-maintenance counter is updated only after Borin proves the durable memory/session content actually changed. If GitHub received a cleanup commit but Borin crashed before clearing the local outbox, the recovered retry still resolves as `NoChanges` instead of creating a maintenance-only duplicate commit.

## Default configuration

```json
{
  "rawGameplayJournalRetentionDays": 7,
  "rawGameplayJournalMaximumMegabytes": 64,
  "rawGameplayJournalMaximumFiles": 48,
  "memoryRepositorySessionSummaryLimit": 180,
  "memoryRepositoryHistoryCompactionEnabled": true,
  "memoryRepositoryHistoryCompactionIntervalCommits": 50
}
```

## Validation on the Windows development machine

Run:

```powershell
.\Tools\Borin\run_long_run_validation.ps1
```

The script verifies the configured retention bounds and runs the Borin deterministic character/self-play test harness unless `-SkipCharacterTests` is supplied. It reports current local Borin-memory and diagnostic disk use but does not delete user files itself.

An actual live soak test is still required to prove operating-system, Git credential, network, BBS timing, and model behavior over many hours/days.
