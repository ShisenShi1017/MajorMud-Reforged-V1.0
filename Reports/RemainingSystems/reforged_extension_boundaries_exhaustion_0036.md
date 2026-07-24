# Remaining Systems Update0036 — Reforged Extension Boundaries

Implemented an executable boundary that separates Classic, Custom, AI, client-only, Librarium-only, and editor-only behavior. Extension access now requires a known definition, allowed surface, compatible world mode, explicit enablement, host authorization for mutation, and matching save/catalog version. Classic isolation is validated deterministically and unknown extensions fail closed.

## Native dependencies

Exact native Classic feature flags, file version markers, save compatibility markers, and Worldgroup module policy remain assigned to the final native pass.
