# SimulatorBuild0002 — Unknown Resolution & Evidence Trace Mode

Status: complete.

This implementation adds an evidence-preserving unknown handling layer to the MajorMUD Reforged simulator. It does not resolve unknowns by guessing and does not claim Worldgroup runtime validation.

## Added

- `unknowns/UNRESOLVED_QUEUE.csv`
- `UnknownManager` runtime component
- placeholder item support for unresolved item references
- trace logging for unresolved/runtime-only behavior
- CLI commands: `unknowns`, `find-unknown`, `simulate-unknowns`, `export-trace`
- interactive shell commands: `unknowns`, `find_unknown`, `simulate_unknowns`, `trace`

## Queue rows

Total unknown/unresolved/runtime-required rows: 27

## Evidence rule

Unknowns are simulated only as placeholders or deferred runtime-required checks. No record is converted to a factual resolution without source evidence.

## Runtime boundary

Worldgroup was not run. Live gameplay behavior remains pending RuntimeValidation.
