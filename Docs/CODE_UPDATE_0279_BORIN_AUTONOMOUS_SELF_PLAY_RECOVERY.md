# Code Update 0279 — Borin Autonomous Self-Play Recovery / Anti-Stuck System

## Goal
Keep Borin playing by himself when an ordinary gameplay plan stops working. Repeated failures, route oscillation, dead-end NPC/quest interactions, economy/equipment loops, unusable model decisions, and server-response timeouts should cause inspection and re-planning rather than ending autonomous play or repeating the same command forever.

## Runtime behavior
- Adds `BorinSelfPlayRecoveryTracker`, a short-term deterministic anti-stuck memory used by Borin's autonomous player loop and reflection journal.
- Adds `selfPlayRecoveryEnabled` to `AI/Config/borin-player.json` and enables it by default.
- Records each autonomous action with its verified before/after room, level, experience, classified terminal events, fresh-prompt status, and whether meaningful progress occurred.
- Supplies the current recovery state to every autonomous decision and reflection alongside Borin's verified personal memory, Librarium guidance, progression plan, equipment plan, and quest/NPC plan.
- Replaces several previous autonomous-loop stop conditions with recovery/re-plan behavior while the actual game session remains healthy.

## Recovery escalation
1. **Retry once when justified.** One failed/no-progress action does not become a permanent rule.
2. **Inspect and verify.** After the same action fails or produces no progress twice in the same verified context, suppress that exact repeat and use a reversible information command such as LOOK, ROOM INFORMATION, INVENTORY, EQUIPMENT, STATS, or EXP.
3. **Change tactic.** Detect two-room movement oscillation, repeated NPC/dialogue dead ends, repeated economy/equipment cycling, repeated combat defeat, or longer no-progress streaks and require a different route/action/strategy.
4. **Abandon the micro-goal.** When the no-progress pattern persists, Borin is explicitly told to drop the failing immediate plan and select a different leveling/quest/equipment/progression objective instead of looping indefinitely.

## Progress clears short-term suppression
The recovery tracker is contextual, not permanent memory. Verified progress such as a new room, XP increase, level gain, item acquisition, equipment change, purchase, or combat victory ends the current no-progress streak. Old temporary failures do not permanently blacklist a valid player command.

## Route/economy/dialogue loop protection
- Detects repeated A/B room oscillation and can suppress the next movement that would continue the same bounce.
- Detects alternating BUY/SELL or WEAR/REMOVE/EQUIP cycles on the same target and tells Borin to refresh live state and re-evaluate the progression value before another irreversible action.
- Detects repeated TALK/ASK/GIVE/READ/EXAMINE/USE attempts that do not create verified progress and forces a different quest/NPC approach.
- Repeated combat defeats are elevated into a strategy-change signal instead of an instruction to re-enter the same fight unchanged.

## Server pacing and timeouts
The game server remains the action clock. There is no per-hour or conversational initiative timer on autonomous gameplay. Borin still waits for authoritative MajorMUD prompt evidence after a command. If the prompt does not arrive, silence is not treated as success or proof of failure: Borin gets at most one reversible perception-recovery attempt and then waits for authoritative server evidence or a real connection-state change rather than flooding state-changing commands.

## Reflection integration
Recovery escalation level 2 or higher is a high-value automatic reflection trigger (subject to the existing reflection safety/cooldown gates). Reflections receive the current short-term recovery evidence so Borin can convert genuine repeated failures into revised strategy while keeping temporary stuck hypotheses separate from durable verified facts.

## Tests added
A deterministic self-play recovery test verifies:
- one justified retry remains allowed;
- the second same-context no-progress repeat is suppressed;
- verified new progress clears the short-term suppression;
- a two-room route oscillation is detected and the next continuation is blocked;
- repeated unusable decisions trigger an inspection/recovery escalation rather than requiring human guidance.

## Verification note
This build environment does not include the .NET SDK, so compilation and the Borin character test harness must still be run on the Windows development machine after applying the cumulative update.
