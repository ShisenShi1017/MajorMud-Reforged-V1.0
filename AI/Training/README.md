# Borin Headless Trainer

Borin's Forge chat owns the trainer. There is intentionally no new UI button.

Say:

- `Borin, start headless training.`
- `Borin, headless training status.`
- `Borin, stop headless training.`

The trainer launches `Reforged.Server` with `--headless-json`, uses the same authoritative runtime as normal play, and exchanges one JSON object per line over stdin/stdout. The default curriculum is 22 hours: 4 hours survival, 6 hours combat, 8 hours navigation/hazard avoidance, and 4 hours macro/economy.

Economy rule: Borin targets **10 gold carried**. When he is at a bank and can deposit an actual carried denomination without dropping below that reserve, he banks the surplus. There is no withdrawal action in the learning action set. If carried wealth falls below 10 gold, he must obtain more by gameplay/loot/selling rather than replenishing from the bank. Buying pauses if it would take him below the reserve.

Learning is persisted in `AI/Training/checkpoints/borin_qlearning.json`. Runtime state is written to `AI/Training/runtime/status.json`; the PID is written to `AI/Training/runtime/trainer.pid` while training is active.

The trainer uses dependency-free tabular Q-learning, allowing it to run with the system Python without installing PyTorch. The policy learns action values per observed game-state bucket, room exploration values, and equipment upgrade signals from actual before/after character state.
