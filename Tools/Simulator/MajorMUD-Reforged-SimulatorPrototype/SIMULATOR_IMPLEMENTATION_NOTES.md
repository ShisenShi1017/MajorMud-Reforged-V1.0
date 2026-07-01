# SimulatorBuild0001 Implementation Notes

Implemented a Python simulator package over the existing RuntimePrep0001-0009 evidence payload.

## Implemented

- Evidence CSV loader
- World/room lookup
- Navigation edge lookup and BFS pathfinding
- Shop inventory lookup/search
- Monster drop/currency lookup
- NPC/dialogue/script candidate search
- Expected spawn-state projection from room population fields
- Stateful interactive shell
- Basic combat projection smoke-test model
- Smoke tests

## Boundary

This remains an expected-model simulator only. It does not run Worldgroup scripts, certify AI behavior, certify combat formulas, or prove live spawn timing.
