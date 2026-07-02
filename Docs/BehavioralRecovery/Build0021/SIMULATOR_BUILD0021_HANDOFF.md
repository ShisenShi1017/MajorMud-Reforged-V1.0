# MajorMUD Reforged — Build0021 Behavioral Recovery Handoff

Build0021 completed as a unified static behavioral inference pass.

Authoritative boundary: no live Worldgroup runtime validation was performed. Original live behavior is not certified unless directly decoded as a static input field.

Use `BehavioralFormulaCatalog_Build0021.csv` as the next source-of-truth for simulator formula promotion decisions. Only rows marked `verified_from_data` should be treated as recovered static behavior. Rows marked `strong_candidate` or `weak_candidate` may be used for expected-model testing but not as original-server proof. Rows marked `runtime_required` remain unresolved.

Next practical step: update the simulator data model to load the verified static fields and optionally expose candidate formulas behind an expected-model flag, while preserving all runtime_required boundaries.
