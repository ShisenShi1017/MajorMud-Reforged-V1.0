# Economy CodeUpdate0164 — Service Cost Finalization

This pass implements deterministic policy stages for additive service adjustments, minimum prices, and final rounding.

## Runtime behavior

- Supports additive adjustments before or after composed multipliers.
- Supports minimum-price enforcement before or after the final rounding boundary.
- Uses checked arbitrary-precision intermediates and checked `long` output.
- Rejects negative final values, overflow, and unsupported native-exact ordering.
- Keeps native-exact execution fail-closed until an original executable trace or controlled transaction capture proves the order.

## Evidence status

The existence of price floors and additive adjustments is represented as a configurable runtime capability. Their exact native placement remains UNKNOWN. No MajorMUD constant or call order was invented.

## Ownership

Economy owns calculation and transaction charging. External trainer, spell, guild, resurrection, inn, and teleport runtimes continue to own their gameplay effects.
