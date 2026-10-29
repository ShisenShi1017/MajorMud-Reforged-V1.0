# Classic monster generation recovery — 2026-10-28

## Source and scope
Read-only disassembly of the supplied original `WCCMMUD.DLL` (SHA256 `5a99e7c34cf9cf58dc6e97fc3ef28f1bd83f9bcec15ca59ad08826d2f21d2077`), cross-checked against original-derived room/monster data. Original RAR/database untouched.

## Recovered native generation inputs
At `0x417417..0x417441`, the native room-generation caller passes:
- room/map identity;
- the room group ID from room structure +`0x560`;
- the direct candidate ID from +`0x468`;
- the minimum monster index from +`0x462`;
- the maximum monster index from +`0x464`.

At `_generate_monster`, `0x424535..0x4245B6`, the native selection scan walks records in the group table (12-byte entries: monster identifier, group ID, index number). Each candidate must match group and optional minimum/maximum index bounds. The first match becomes provisional selection. For each subsequent eligible entry, native 1..100 rolls below 30 replace the provisional candidate; rolls of 99/100 end the scan.

At `0x424482..0x4244BA`, generation resolves the linked population controller and checks its area-wide population counter (+`0x5C0`) against its cap (+`0x5BE`). Room-local generated count (+`0x606`) is separate.

## Current Reforged changes
- `RuntimeNpcDefinition.IndexNumber` now retains the original index where it is available in `public_reference_0139.fields.index_number`.
- `RuntimeNpcSpawn.FromJson` accepts source-qualified `spawn_model.group_index` values such as `21-4-4`, or `monster_index_min` plus `monster_index_max`. Wrong-group/invalid bounds are rejected.
- Qualified lairs use the recovered native 30% replacement / 99% early-stop selection thresholds.
- Linked spawning respects an area-wide capacity on initial materialization and fixed-slot respawn admission.
- The compiler validates malformed source qualifications and smoke tests check indexed admission.
- The server smoke diagnostics explicitly report group spawns still lacking an authoritative native index range.

## Remaining parity blockers — do not invent data
1. Current original-derived room JSON does not expose the native offsets +`0x462`/+ `0x464`. For example Map 1 Room 2342 only has `monster_type=21`, not its native index range. **These values must be extracted from original authoritative room data**, and promoted to the Librarium spawn source. Do not substitute a hardcoded silverfish or fungus lizard home room.
2. Ordinary group NPCs still use cached materialized instance slots, with `HomeRoomId` restoration in `RuntimeNpcSpawnLifecycle.cs`; that does **not** implement native dynamic generation. Correct this only with the original regeneration admission, membership/counter and persistence lifecycle contract in place. Bosses, direct NPCs, summons and scripted entities have distinct owners.
3. The current roll uses `Random.Shared` rather than the native process/thread RNG stream. Native-host RNG integration and global group-table enumeration ordering need exact evidence.

## Regression command
`dotnet run --project Tools/RealmGateValidator/DotNet/RealmGate.Validation.csproj -- --smoke-classic-spawn-eligibility`

Run locally after pulling main. Do not infer success merely from static review. Verify River Room 2342 and connected area, Map 3 original fungus lizard regen locations, native area caps, and NPC movement across rooms without forced home-room respawns.
