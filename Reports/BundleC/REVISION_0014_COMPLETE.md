# Bundle C Revision 0014F — Runtime Closure Fix

Directly closes the three failures reported by the Bundle C completion command on the A–D-installed repository.

- C1 now validates the live `EquipmentModifierTotals` aggregation actually consumed by `RuntimeEquipmentRuntimeAuthority`.
- C8 validates decoded NPC currency ranges, item percentages, room-ground materialization, hidden cleanup state, and transient search replacement without requiring the removed persistent discovery model.
- C9 installs the corrected Classic SEARCH runtime: every command rerolls every eligible hidden target, replaces the prior result set, never persists discovery, and keeps items hidden until pickup.
- Equipment slot resolution remains decoded-data-only; name/type slot guessing is not restored.
- Revision retains the complete typed Bundle C service/item authority introduced in Revision 0014.
