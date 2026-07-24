# Final Native Closure Update0032 — WCC/WBB Knows Sentinel Gate

## Proven

The helper at `0x0040AAC5` pushes mode `0` and the distribution-specific sentinel path (`wccknows.sys` or `wbbknows.sys`) into the imported access routine at `0x0040AACC`. A zero native return becomes Boolean true; any nonzero return becomes false. The helper does not open or read the file.

There is exactly one recovered static caller in each supplied MMUD module, at `0x0040D661`. That caller immediately tests the helper result and inverts it: sentinel present returns false for that validation branch, while sentinel absent returns true. WCC and WBB preserve the same instruction path and addresses; only the embedded distribution prefix differs.

## Closure effect

`wccknows.sys` is now exhausted as a WCC-side support asset. It is an existence-only sentinel in the recovered MajorMUD path, not a token file, configuration payload, or text database. Reforged must not read or invent contents for it.

## Not claimed

This pass does not rename the broader surrounding validation routine or infer why the sentinel disables that one branch. Any external Worldgroup handling not visible in MMUD remains outside this proof.
