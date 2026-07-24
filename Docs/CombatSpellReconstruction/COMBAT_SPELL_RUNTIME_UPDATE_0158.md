# Combat & Spells Runtime Update 0158

## Executable runtime advancement

This pass introduces physical combat-instance ownership and deterministic round scheduling. A combat instance owns its room, participants, round number, monotonic clock, transitions, and stable insertion sequence. Character and NPC participants require canonical identifiers and an evidence-backed scheduling policy.

Due turns are ordered by next-action tick, descending initiative, insertion sequence, and canonical actor ID. A due batch advances the round exactly once and atomically advances every selected participant's next-action clock. Participant defeat, flee, disconnect, or removal is terminal. When fewer than two promoted opposing actor kinds remain, the instance enters termination and removes remaining active participants.

No production initiative or action-delay values were inferred. Missing values, missing evidence, invalid actor ownership, duplicate participants, and tick regression fail closed. Persistence remains unactivated.

## Validation

The pass validates canonical combat, room, character, and NPC IDs; promoted scheduling gates; stable tie-breaking; round batching; terminal cleanup; Python syntax; C# structural references; cumulative preservation; manifest hashes; and ZIP integrity.
