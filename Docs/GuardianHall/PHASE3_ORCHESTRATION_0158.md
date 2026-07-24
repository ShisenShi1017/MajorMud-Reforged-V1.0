# Guardian Hall Phase 3-0158 — Scheduled Orchestration

Adds a persisted Guardian Activity Center with queued, running, completed, failed, blocked, paused, and cancelled job states. Realm Keeper, Wanderer, Analyst, and Archivist have role-safe scheduled execution. Wanderer requires a real assigned session. Worldsmith remains manual and approval-gated. Interrupted running jobs are re-queued after restart; job history is bounded to 100 records.
