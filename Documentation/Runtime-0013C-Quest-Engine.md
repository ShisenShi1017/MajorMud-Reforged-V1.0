# Runtime-0013C — Quest Engine

## Purpose

Runtime-0013C adds a quest-state engine designed around ability flags. The engine supports quest definitions, objectives, progress tracking, start/complete operations, required ability checks, and reward ability grants.

## Important Accuracy Boundary

The canonical `Data/quests.json` in this package is currently empty. The quest engine is therefore validated with a synthetic non-promoted validator fixture. No invented quest content is promoted into `/Data`.

## Added Files

- `Runtime/quests/quest_engine.py`
- `Runtime/quests/__init__.py`
- `Sim/validators/validate_ability_quest_engine.py`

## Runtime Model

Quest progression is represented as character state, not global data mutation. Quest rewards may grant ability flags. Quest requirements may check ability flags.

## Persistence Implication

Future save/load must persist quest progress, objective counters, completion state, and any quest-granted ability flags.
