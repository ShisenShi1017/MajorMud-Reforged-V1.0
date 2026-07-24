# Final Native Closure Update0026 — Support Distribution Coverage

## Proven closures

- WCC ships 47 HSE house-description overrides.
- WBB ships 133 HSE house-description overrides.
- The 47 WCC IDs are a strict subset of WBB; WBB contains 86 additional IDs.
- Every shared HSE payload is byte-identical.
- All seven STO overrides and all three BIN screens are also byte-identical across the supplied WCC/WBB copies.

## Compatibility rule

The filename prefix identifies distribution ownership even where payload hashes match. Reforged must not silently use a WBB-only HSE override while running a WCC profile. Missing support assets remain on the selected distribution's native/default path until that fallback is independently proven.
