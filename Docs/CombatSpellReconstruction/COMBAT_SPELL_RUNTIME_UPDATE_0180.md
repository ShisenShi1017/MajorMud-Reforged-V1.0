# CodeUpdate0180 — Combat Ability and Item Proc Execution

Adds an evidence-gated execution boundary for activated combat abilities and item procs. Ability ownership remains with the Ability subsystem; inventory and equipment ownership remain with Character, NPC, and Item systems. Combat owns validation at execution time, deterministic proc ordering, resource/cooldown decisions, and atomic mutation handoff.
