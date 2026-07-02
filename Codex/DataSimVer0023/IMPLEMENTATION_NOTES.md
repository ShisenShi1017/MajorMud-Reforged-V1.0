# DataSimVer0023 Implementation Notes

Expected simulator ordering for future tests:

1. Evaluate quest gate / persistence status.
2. Evaluate explicit timer or global respawn eligibility.
3. Check room/spawn occupancy to avoid duplicate spawn.
4. Spawn only when all gates pass.

This is not verified original engine behavior. It is a safe expected model for simulations.
