# ServerSim0036 Scheduler Mapping

CodeUpdate-0062 maps ServerSim0036 planning evidence into diagnostic scheduler channel registrations.

## Registered Diagnostic Channels

| Channel | Purpose |
|---|---|
| PlayerState | Future concurrent player activity and condition state coordination. |
| Movement | Future movement, follow, and room transition scheduling. |
| CombatRound | Future combat engagement, action batches, damage, death, XP, and continuation checks. |
| RegenHp | Future hit point recovery cadence. |
| RegenManaKai | Future mana and kai recovery cadence. |
| Spawn | Future timed, instant, boss, quest, scripted, and event spawn policies. |
| Cleanup | Future lifecycle maintenance work. |
| StatusEffect | Future condition and effect duration handling. |
| ScriptEvent | Future textblock, trigger, quest, and script event dispatch. |
| AI | Future AI and NPC decision scheduling. |

## Runtime Status

All channels are diagnostic registrations only in CodeUpdate-0062.

Runtime execution is disabled for every channel. The Server still does not start sockets, sessions, a world loop, combat, spawning, player state updates, status-effect ticking, script dispatch, AI, or gameplay.

