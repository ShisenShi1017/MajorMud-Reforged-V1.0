# CodeUpdate0195 — Companion Combat Assistance Ownership

Implemented an exact non-native companion combat-assistance boundary. A living companion accepts a target only when its persisted owner matches, assistance is explicitly authorized, the target is alive in the same room, and detectability and hostility are explicitly supplied. Rejections do not mutate current target or threat state.

Not inferred: native pet commands, assist radius, faction rules, threat transfer, targeting priority, duration, dismissal, or combat formulas.
