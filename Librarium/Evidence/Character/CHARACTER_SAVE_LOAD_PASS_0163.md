# Character Save/Load Phase 2 — CodeUpdate0163

Classification: **STRONGLY CORRELATED**.

The Reforged runtime now owns startup recovery sweeping and deterministic five-minute autosaves. Every primary Character image is validated during repository startup; a valid backup is recommitted before gameplay, while invalid primary plus invalid/missing backup fails closed. Active persistent sessions autosave in canonical Character-ID order and persist room, level, autosave timestamp, save reason, sequence, and compatibility boundary.

The five-minute interval is an explicit Reforged lifecycle policy. It is not promoted as the unknown native MajorMUD cadence. Native binary serialization and native crash journaling remain UNKNOWN for the final native pass.
