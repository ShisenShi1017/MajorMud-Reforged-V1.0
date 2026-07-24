# Worldgroup Character-Target Spell Dispatch — Update0016

Direct disassembly bounds `wccmmud.dll!0x00444712` as the five-argument character/creature-target spell execution dispatcher entered by the shared CAST/INVOKE wrapper. This pass closes its argument contract and preflight gates without inventing the still-unrecovered effect body.

Proven entry behavior includes session-record resolution, null rejection, caster state and fear gates, class-specific restrictions, room-context resolution, transient-state clearing, self-target rejection for target kinds 0/1/2, and the final configuration/room/target eligibility gate before effect processing.

The remaining body from `0x00444A06` to `0x00447110` contains the chance, cost, effect, and output logic and is the next boundary. No direct textblock call is claimed in this pass.
