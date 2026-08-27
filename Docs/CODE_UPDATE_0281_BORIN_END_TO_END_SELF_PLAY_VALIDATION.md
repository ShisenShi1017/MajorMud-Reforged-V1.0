# Code Update 0281 — Borin End-to-End Self-Play Validation and Runtime Diagnostics

## Purpose

Code Updates 0271–0280 built the autonomous-player stack in layers. 0281 adds a composed validation pass and bounded runtime diagnostics so the development PC can prove those layers cooperate as one player lifecycle instead of only passing isolated tests.

## Integrated deterministic lifecycle

`BorinEndToEndSelfPlayValidation` runs without a model, BBS connection, credentials, or Borin's real memory. The existing `BorinCharacterTestHarness` now includes this scenario in every run.

The synthetic lifecycle validates that:

1. progression planning combines Borin's verified personal leveling experience with Librarium guidance;
2. equipment/shop planning requires live verification and protects irreversible possession changes;
3. decoded quest/NPC flow evidence remains reference guidance and advances one live-verified step at a time;
4. ordinary combat/equipment gameplay stays autonomous;
5. session-destruction and command batching remain blocked;
6. stale inventory evidence blocks irreversible possession changes;
7. two same-context no-progress failures suppress a third blind repeat;
8. genuine room/XP progress clears the immediate stuck hypothesis;
9. a cleanup warning checkpoints the active autonomous assignment;
10. session memory is consolidated before restart/resume;
11. an actual cleanup disconnect establishes the durable memory-sync boundary;
12. verified post-cleanup state marks the lifecycle resumed;
13. high-level runtime diagnostics redact credential-like values.

This is deliberately model-free and network-free. It verifies deterministic contracts before the live Borin character is allowed to expose runtime-only defects.

## Bounded live runtime diagnostics

`BorinSelfPlayRuntimeDiagnostics` records high-level autonomous lifecycle events under:

`Release\Logs\BorinBrain\SelfPlayDiagnostics`

It records operational state only, including autonomous start/stop, readiness blocks, proposed command names, safety/recovery rejection, command-send pacing, evaluated outcomes, evidence timeouts, cleanup signals, reconnect retries, and cleanup resume readiness.

It does **not** record:

- terminal transcripts;
- model prompts or hidden/model reasoning;
- passwords, tokens, credentials, or authentication material;
- Borin's durable memory contents;
- Librarium record bodies.

Credential-like key/value content is redacted as a second defensive boundary. Diagnostics failures never stop gameplay.

Default bounds:

- enabled: true;
- active JSONL file maximum: 1 MiB;
- rotated diagnostic files retained: 5;
- in-memory diagnostic ring: 200 events;
- duplicate identical state events inside five seconds are collapsed.

These limits keep the validation trace useful without allowing a long-running player to create an unbounded diagnostic archive.

## Development-PC validation command

From the repository root:

```powershell
.\Tools\Borin\run_self_play_validation.ps1
```

For the full repository health gate first:

```powershell
.\Tools\Borin\run_self_play_validation.ps1 -FullRepositoryValidation
```

To rebuild/evaluate Borin's MajorMUD knowledge catalog as part of the focused run:

```powershell
.\Tools\Borin\run_self_play_validation.ps1 -BuildMajorMudKnowledge
```

The script requires the .NET 8 SDK. It restores/builds `Borin.CharacterTests`, runs the deterministic harness, writes a timestamped JSON validation report, and prints the location of the bounded live self-play diagnostic directory.

## Live-test interpretation

After deterministic validation passes, run Borin normally. If he stops playing, loops, fails to resume after cleanup, or appears to wait indefinitely, inspect the latest `Borin-SelfPlay-*.jsonl` diagnostics file. The trace is intended to show the last high-level lifecycle transition without exposing credentials or copying raw game output.

## Packaging rule

This cumulative ZIP retains every file created or modified by Code Updates 0271–0281 exactly once, at its newest version. Runtime logs, actual `AI\Memory` contents, generated binaries, build output, and unrelated untouched repository files are excluded.
