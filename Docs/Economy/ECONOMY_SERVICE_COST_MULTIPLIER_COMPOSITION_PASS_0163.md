# Economy CodeUpdate0163 — Service Cost Multiplier Composition

This pass resolves the Reforged ordering dependency between level/rank scaling, provider markup, and discount/surcharge when all three are supplied as multiplicative basis-point factors.

## Runtime behavior

The engine multiplies the base value and all three factors using arbitrary-precision integer arithmetic, divides by `10000^3`, and rounds exactly once using the selected policy. Because multiplication is commutative, permutations of these factors produce the same result.

## Safety

Negative factors are rejected. Conversion back to `long` is checked. Native-exact mode remains fail-closed when original intermediate rounding, additive modifiers, caps, or minimum-price stages are required.

## Evidence status

The factor composition is strongly correlated and active for policy-driven execution. The exact original MajorMUD intermediate rounding sequence remains unknown and is retained for native trace closure.
