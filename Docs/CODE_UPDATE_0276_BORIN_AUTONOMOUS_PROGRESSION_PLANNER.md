# Code Update 0276 - Borin Autonomous Leveling and Progression Planner

## Goal

Give Borin a longer-horizon autonomous player plan so he does not choose isolated commands forever. The progression planner combines current verified character state, Borin's verified personal gameplay history, structured Librarium guidance, and his revisable reflection journal.

## Runtime behavior

- The planner runs before each autonomous player decision.
- Live server state remains authoritative for the immediate turn.
- Low health and combat take priority over grinding or travel.
- If level/experience state is not known, Borin is directed to establish it through normal player information commands.
- Verified personal leveling results are preferred when they remain relevant to Borin's current level and live condition.
- When Borin lacks useful personal leveling evidence, conservative Librarium NPC/spawn candidates become scouting targets rather than guaranteed grinding locations.
- Equipment, quest, shop, spell, and class references can compete with raw XP grinding when they plausibly improve survival or progression.
- The plan is re-evaluated after meaningful XP gain, level changes, death/near-death, equipment changes, target-area arrival, or evidence that the current plan is inefficient.
- The planner never sends a command and never grants permission to invent a route, room, exit, or server fact.

## Authority order

1. Current verified live server evidence.
2. Verified personal character experience.
3. Proven Librarium game-world evidence.
4. Strongly-correlated/reference Librarium clues.
5. Borin's own reflection/strategy hypotheses.

## Configuration

`progressionPlannerEnabled` defaults to `true` in `AI/Config/borin-player.json`.

## Verification

`BorinCharacterTestHarness` now includes a deterministic progression-planner boundary test covering low-health priority, verified personal leveling memory, Librarium target retention, and the no-command/no-invented-route boundary.
