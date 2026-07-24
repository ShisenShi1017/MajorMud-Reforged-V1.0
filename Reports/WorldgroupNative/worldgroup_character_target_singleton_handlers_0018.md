# Worldgroup Character-Target Singleton Handlers — Update0018

Direct disassembly now proves the first concrete textblock edge below the character-target spell dispatcher. Effect ID `148` enters handler `0x0044596B`, which calls `0x00471D55` (`_perform_text_block_as_special_command`) with the current effect value and caster session index. The handler then emits the normal spell-success output once and marks the shared display flag.

The pass also closes the shared post-effect loop. Up to ten effect slots are processed. Effects `140` and `141` populate shared values consumed by a post-loop host action, while effects `151` and `164` populate a chained-spell identifier and percentage threshold. A successful `0..100` roll resolves temporary spell data, recursively invokes `0x00444712`, and frees the temporary record.

Resource handling is now structurally bounded: player field `+0xBA` is compared and conditionally reduced by spell field `+0xBC`; spell field `+0xF0` is compared against player field `+0x602`; hostile completion can engage autocombat; and secondary statistics are recalculated before return. Most singleton effect names remain intentionally unassigned until independent record/string evidence proves them.
