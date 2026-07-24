# Combat & Spells Runtime Update 0165

CodeUpdate0165 adds the evidence-gated combat movement boundary. A successful voluntary, forced, or fear-driven movement request now validates canonical combat, actor, room, destination, and exit ownership; checks promoted blockers; requires an explicit promoted escape outcome where configured; cancels dependent queued and claimed actions; removes temporary threat; transitions the participant to `Fled`; and emits a deterministic room-exit handoff for the world/room owner.

The pass deliberately does not infer native escape chance, direction selection, pursuit, opportunity attacks, or failed-flee round/resource consumption. Those branches remain visible and fail closed.
