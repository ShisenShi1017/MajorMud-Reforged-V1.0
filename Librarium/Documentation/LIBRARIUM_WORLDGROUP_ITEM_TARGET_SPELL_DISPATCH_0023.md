# Librarium — Worldgroup Item-Target Spell Dispatch Update0023

## Proven boundary

`wccmmud.dll!0x00447110` is the item/inventory-target spell or power dispatcher selected by the shared CAST/INVOKE wrapper. The routine ends immediately before the room/area dispatcher at `0x00449232` and uses four cdecl arguments: the definition record, caster session index, item/inventory handle, and invocation mode.

## Resolution and validation

The caster is resolved through `0x004320D9`; the item or inventory instance is resolved through `0x0043248A`. Null records terminate with zero. The definition type at `+0xCC` is mode-sensitive: mode zero accepts types 6 or 8, while nonzero mode accepts types 6 or 7. The target instance must resolve into the caster's current room context, the caster level at `+0x94` must satisfy the definition requirement at `+0xBE`, and room/configuration plus compatibility gates run before effects.

## Effect and resource ordering

The dispatcher processes up to ten effect slots and contains a bounded outer retry/selection loop of twenty iterations. Effect branches converge on shared completion state. A chained definition can be resolved through `0x00476F7D`, recursively dispatched back into `0x00447110`, and then released through `0x00476F22`.

On finalized execution, cost `definition +0xBC` is subtracted from caster resource `+0xBA`. The signed requirement at `definition +0xF0` contributes a half-value subtraction from caster field `+0x602` where the branch applies. Local output is flushed through `0x0046B170`, room output uses `0x0046B010`, and the final host/action notification path may call `0x0044C85F`.

## Next boundary

The room/area dispatcher begins at `0x00449232`. Its entry confirms definition types 6 or 7, resolves the caster, applies state gates, and resolves class context. Its complete effect body remains the next pass rather than being inferred here.
