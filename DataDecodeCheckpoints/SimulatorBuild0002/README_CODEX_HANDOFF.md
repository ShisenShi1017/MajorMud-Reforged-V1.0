# MajorMUD Reforged — Codex Handoff

This package is the cumulative handoff for continuing simulator development. It preserves the verified static chain, RuntimePrep0001-0009, and the implemented simulator through SimulatorBuild0002.

## Current state

- Static decode/certification: complete through DataDecode0110.
- Runtime preparation: complete through RuntimePrep0009.
- Simulator prototype: implemented through SimulatorBuild0002.
- Live Worldgroup validation: not started.

## Runtime boundary

Do not claim live gameplay equivalence. The simulator currently operates on the expected runtime model derived from MDB/CSV/WBB/WCC/static evidence. Worldgroup runtime observations are still required for live behavior certification.

## Simulator entry point

Primary script:

```bash
python simulator/MajorMUD-Reforged-SimulatorPrototype/run_simulator.py --help
```

If running from inside the simulator directory:

```bash
cd simulator/MajorMUD-Reforged-SimulatorPrototype
python run_simulator.py --help
```

## Evidence layout

- `evidence/static/` contains DataDecode0110, which carries the final static certification and static evidence chain.
- `evidence/runtimeprep_archives/` contains RuntimePrep0001-0009 checkpoint ZIPs.
- `simulator/MajorMUD-Reforged-SimulatorPrototype/` contains the current implemented simulator and extracted RuntimePrep evidence payloads.
- `supporting_docs/` contains quest-support documentation.
- `source_archives/` contains the Reforged source archive and the prior simulator build archive.

## Standing rules

Never fabricate evidence, hashes, metrics, reports, manifests, or ZIPs. Preserve unresolved issues instead of guessing. Keep runtime observations separate from expected-model simulation.
