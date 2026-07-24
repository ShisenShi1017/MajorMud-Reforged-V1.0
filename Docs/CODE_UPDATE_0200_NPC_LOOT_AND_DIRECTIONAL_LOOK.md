# Code Update 0200

- Added `look <direction>` / `l <direction>` adjacent-room inspection without movement.
- Added NPC corpse creation on live-combat defeat.
- Added visible NPC corpse rendering in the room.
- Preserved loot owner and expiration metadata for the existing item/loot runtime.
- Does not invent item drops for NPCs whose generated Classic record explicitly has no authoritative item-drop table.
