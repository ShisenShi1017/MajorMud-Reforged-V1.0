# SimulatorBuild0002 Handoff Preservation

This folder preserves the user-provided `MajorMUD-Reforged-CodexHandoff-SimulatorBuild0002.zip` package as repository evidence.

## Source Package

- Source path: `C:\Users\djkao\Downloads\MajorMUD-Reforged-CodexHandoff-SimulatorBuild0002.zip`
- ZIP SHA-256: `66DF6BF0E1885A96C1B3BFB9A9D535FEDA8FA9D025A807509175B4C644D758DA`
- Top-level folder in package: `CodexHandoff-MajorMUD-Reforged-Simulator/`

## Preserved Contents

- Handoff docs: `README_CODEX_HANDOFF.md`, `CURRENT_STATUS.md`, and `NEXT_SIMULATION_TASKS.md`.
- Handoff manifests under `manifests/`.
- RuntimePrep0001-0009 archival ZIPs under `evidence/runtimeprep_archives/`.
- DataDecode0110 static certification archive under `evidence/static/`.
- Quest support document under `supporting_docs/`.

## Excluded Contents

The embedded full repository archive `source_archives/MajorMud-Reforged-V1.0-main (2).zip` was intentionally not committed. GitHub remains the source of truth, and stale repository-in-repository archives should not become project truth.

Generated Python `__pycache__` files were also excluded from the committed simulator tool copy.

## Boundary

SimulatorBuild0002 is an expected-model simulator handoff, not live Worldgroup runtime validation. It can support future server work by providing testable expected behavior scenarios and data relationship evidence, but it cannot certify exact live combat, timers, scripts, AI choices, drop probabilities, or hidden runtime behavior.
