# CodeUpdate0183 — Combat and Spell Persistence & Recovery

Adds an evidence-gated persistence boundary for active effects, cooldowns, recovery timers, combat state, threat, pending actions, casting state, summons, and control effects. Snapshot commits are transactional, timers are represented as remaining-tick offsets, recovery validates integrity and world time, and nonpersistent state produces explicit cleanup requests instead of silent restoration.
