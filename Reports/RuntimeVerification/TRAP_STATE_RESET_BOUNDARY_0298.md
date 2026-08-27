# Trap State / Reset Boundary — Update 0298

## Native storage now fixed

Stock MajorMUD RoomRec evidence establishes ten exit slots with signed 16-bit `RoomType`/`Para2` and signed 32-bit `Para1`/`Para3`/`Para4`. Trap types are RoomType 9 (damage) and RoomType 24 (spell). Para2 is the trap state: 0 Active, 1 Inactive, 2 Hidden, 3 Active movement, 4 Inactive movement.

## Runtime boundary

`DISARM TRAP <direction>` now requires the character's Traps/DisarmTraps grant. MajorMUD help says ordinary characters can still discover traps through SEARCH, while a traps expert has a higher find chance and may attempt to disarm a found trap. Exact find/disarm probability is not reused from generic hidden-object searching.

## Still unresolved

The MMExplorer Rooms export preserved rendered trap operands but not native Para2. Per-exit state therefore requires stock WCCMP002 RoomRec data. Para3/Para4 message ownership also remains unresolved because retained Nightmare Redux documentation and editor labels conflict. Trap transitions, failed-disarm triggering, reset/rearm interaction, and simultaneous world-reset/cleanup ordering remain fail-closed.
