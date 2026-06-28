# Reforged Decision Log

This file records important design decisions and the reasoning behind them.

## Decision 001: Use Repository Documentation As Replacement Design History

### Date

2026-06-27

### Decision

Because the original architecture/design PDF was not available, the project will maintain living documentation under `Docs/`.

### Reason

Future development sessions need a reliable way to continue from the prior context without losing project direction.

### Consequence

The `Docs/` folder becomes part of the project source of truth and should be updated by every official patch.

## Decision 002: Official Patches Run From Extracted Patch Folder

### Date

2026-06-27

### Decision

Every official patch must include an `Apply-*.bat` designed to run from the extracted patch folder.

### Reason

This creates a consistent patching process and avoids requiring the user to run scripts from inside the project root.

### Consequence

Patch packages should contain a payload folder and an apply script that copies only targeted files into the chosen project folder.
