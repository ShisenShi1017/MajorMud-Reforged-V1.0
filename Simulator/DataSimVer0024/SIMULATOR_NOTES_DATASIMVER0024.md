# DataSimVer0024 Simulator Notes

This pass extends simulator documentation only.

## Modeled Hypothesis Buckets
1. Normal creature global respawn cycle, approximately 15 minutes.
2. Explicit monster timers start on death and continue through daily reset.
3. Quest monsters may require player or world quest state.
4. Boss/unique monsters may require special spawn conditions.
5. Room occupancy may affect spawn eligibility, but no direct rule is confirmed.
6. Cleanup operates separately from monster respawn: cleanup may delete or hide items; monster timers remain independent.

No simulator assumption in this file is promoted to `/Data`.
