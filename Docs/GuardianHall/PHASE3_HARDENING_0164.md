# Guardian Hall Phase 3-0164 — Final Hardening

This pass adds non-destructive persistence and runtime safety validation to Guardian Hall.

## Persistence

Guardian Hall state is written to a temporary file, the previous primary state is copied to `guardian-hall-state.json.backup`, and the temporary file atomically replaces the primary state. A malformed primary JSON file falls back to the backup and records that recovery in migration status.

## In-application self-test

The Guardian Activity Center now exposes **Run Hall Self-Test**. The test verifies:

- all five Guardian roles are loaded;
- Wanderer retains a character assignment;
- Worldsmith is not targeted at Live Realm;
- Analyst remains a non-character role;
- Archivist remains preview-only;
- no unattended Worldsmith job is running;
- enabled Realm Keeper and Wanderer connectors use valid HTTP/HTTPS URLs and bounded timeouts;
- Guardian Hall state survives a real save/reload round trip.

Results and the last run time persist with schema version 7.

## Release status

Static validators and XAML parsing pass. A compiled Windows Release remains dependent on running the combined installer on a Windows machine with the .NET 8 SDK.
