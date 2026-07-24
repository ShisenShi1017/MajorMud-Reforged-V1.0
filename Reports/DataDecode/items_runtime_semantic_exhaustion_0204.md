# Items Runtime Semantic Exhaustion — CodeUpdate0204

## Final pre-native result

The Items subsystem has reached the native-only boundary. Every remaining unresolved Item behavior now depends on WCC/WBB records, native DLL or executable control flow, native save layouts, native RNG/scheduling, exact native message tables, or equivalent leftover-file evidence. No additional Classic behavior can be activated safely from the current repository and projected data without invention.

The cumulative runtime already covers evidence-safe identity, ownership, transfer, stacking infrastructure, equipment, portable containers, charges and durability primitives, corpses, resurrection, banking, merchants, resets, NPC inventory and loot reconciliation, quest and event items, limited-item enforcement, persistence validation/recovery, cross-save coordination, safe state repair, and administrative operations.

## Native dependency classes

- Item record layouts, category/type codes, flags, restriction bits, and native equipment-slot edge cases.
- Classic stackability, maximum stacks, split/merge ordering, and overflow outcomes.
- Charge-bearing mappings, per-use costs, recharge behavior, and zero-charge outcomes.
- Durability eligibility, wear formulas, breakage, repair, and destruction/transformation.
- Native loot mappings, RNG, quantity rolls, gold scaling, and conditional drops.
- Native room, container, NPC, merchant, quest, and event reset scheduling.
- Native character/world save layouts, field widths, journals, checksums, backups, migrations, and crash recovery.
- Exact command ordering, messages, failure outputs, sysop permissions, and native exceptions.
- Limited/unique scope and release edge cases, corpse permission timing, resurrection ordering, and native container behavior.

## Safety boundary

Unknown native behavior remains inactive or explicitly evidence-gated. Ambiguous mutations fail safely. No flag, formula, mapping, timer, ordering rule, message, save field, or destructive cleanup policy is inferred merely because a field or runtime hook exists.

## Completion status

Pre-native Items semantic exhaustion is complete. Classic Items semantic exhaustion remains open only for the final WCC/WBB/native DLL and leftover-file pass.
