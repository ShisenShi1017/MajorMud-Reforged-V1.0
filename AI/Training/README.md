# Borin Continuous Headless Trainer + Tester

Borin's Forge chat owns the trainer. There is intentionally no new UI button.

Say:

- `Borin, start headless training.` — starts continuous unattended training.
- `Borin, headless training status.` — shows the current cycle, phase, elapsed time, learning steps, economy state, and tester counts.
- `Borin, what have you found broken?` — shows the highest-priority defects found by the training tester.
- `Borin, training tester report.` — same defect-report path.
- `Borin, stop headless training.` — requests a graceful stop and saves the latest checkpoint/report.

The trainer launches `Reforged.Server` with `--headless-json`, uses the same authoritative runtime as normal play, and exchanges one JSON object per line over stdin/stdout. It does not need a normal client session or MegaMUD connection.

Beginning with the completed CodeUpdate0369 parity baseline, Borin also owns a **100% Classic parity regression preflight**. Before unattended learning starts, and again at every completed learning-cycle boundary, the trainer runs both `--smoke-classic-final-strong-closure` and `--smoke-classic-parity-closure`. The cycle-boundary check gracefully restarts the headless runtime first so the smoke gates run against an unlocked build tree and persistence is exercised as part of the long run. After the server comes back, Borin probes `HELP TOPICS`, the native `HELP TOP` prefix path, `TOPTEN`, `TOP 10`, and `TOP 10 GANGS` through the same live player command dispatcher. Any smoke-gate, scorecard, or player-probe regression is recorded as a normal training-tester defect instead of being learned around.

Training is continuous. One default cycle is four hours and contains eight 30-minute phases:

1. Survival & Recovery
2. Exploration & Hazard Avoidance
3. Combat & Tactics
4. Spells, Powers & Item Use
5. NPCs, Dialogue & Quests
6. Inventory, Equipment & Item Care
7. Progression & Character Mastery
8. Economy & Macro Strategy

After phase 8, Borin immediately begins the next cycle and keeps training until explicitly stopped.

Economy rule: Borin targets **10 gold carried**. When he is at a bank and can deposit an actual carried denomination without dropping below that reserve, he banks the surplus. There is no withdrawal action in the learning action set. If carried wealth falls below 10 gold, he must obtain more through gameplay, loot, combat, or selling rather than replenishing from the bank. Buying pauses if it would take him below the reserve.

The tester runs alongside learning. It aggregates reproducible runtime/protocol failures, server restarts, unsupported executable routes, suspicious empty responses for state-query commands, invalid state invariants, and blocked/broken exit data. Each defect records severity, category, command, room, phase, cycle, count, first/last seen time, and a response/error excerpt.

Persistent learning is stored in `AI/Training/checkpoints/borin_qlearning.json`.

Generated runtime files:

- `AI/Training/runtime/status.json` — live training status, including the last Classic parity regression result.
- `AI/Training/runtime/parity_regression.json` — 200/200 scorecard check, smoke-gate results, and deterministic player-visible parity probes.
- `AI/Training/runtime/trainer.pid` — active trainer PID.
- `AI/Training/runtime/stop.request` — graceful-stop request (normally removed automatically).
- `AI/Training/runtime/defects.json` — machine-readable aggregated tester findings.
- `AI/Training/runtime/defects.md` — human-readable tester report.
- `AI/Training/runtime/trainer.log` — headless server stderr and trainer diagnostics.

The trainer uses dependency-free tabular Q-learning. It does not require PyTorch or Stable-Baselines3.
