# Worldgroup Character-Target Effect Dispatch — Update0017

Direct disassembly closes the primary effect-selector topology inside `wccmmud.dll!0x00444712`. The dispatcher reads a 16-bit effect identifier from the spell/power definition array at offset `+0xD8`, bounds direct selectors to `0x00..0xA4`, compresses them through a 165-byte selector map at `0x004457D5`, and dispatches through a 33-entry absolute jump table at `0x0044587A`.

This proves that character-target effects are not a single monolithic formula. They are partitioned into 33 implementation classes. Thirty-one classes are singleton special handlers, one class is a shared no-effect/continue path, and one class is the broad default path. IDs above `0xA4` do not enter this direct switch.

The pass intentionally does not assign semantic names to effect IDs without independent record or string evidence. Resource deductions, output ordering, and textblock linkage remain open below the individual handler branches.
