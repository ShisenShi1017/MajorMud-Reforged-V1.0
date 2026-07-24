# Combat & Spells Runtime Update 0177

Implements the evidence-gated spell summoning transaction boundary. The runtime validates canonical definitions, ownership, room/world/duplicate limits, duration, optional combat insertion, threat inheritance, persistence policy, and cleanup registration before committing any world mutation. Every downstream owner prepares first; partial commits roll back in reverse order.

Exact production summon definitions, limits, durations, persistence, and native coefficients remain fail-closed for the final evidence/native pass.
