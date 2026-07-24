# Code Update 0196 — Arena Summoned NPC AI-State Fix

## Failure
Entering Newhaven Arena invoked the Classic rotation summon. `SummonNpc` inserted the new NPC into `_npcs`, but did not insert a matching entry into `_npcAi`. Later living-world processing indexed `_npcAi[npc.InstanceId]`, causing a `KeyNotFoundException` for an ID such as `classic-newhaven-arena-rotation:1:1`.

## Correction
`RuntimeLivingWorldService.SummonNpc` now creates an idle `RuntimeNpcAiState` immediately after creating the summoned NPC. The state uses the summoned room as both current and home room and is available before events or persistence run.
