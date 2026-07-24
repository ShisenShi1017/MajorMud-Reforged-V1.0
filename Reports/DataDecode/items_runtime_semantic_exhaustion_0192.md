# Items Runtime Semantic Exhaustion — CodeUpdate0192

## Executable advancement

CodeUpdate0192 adds an opt-in validated persistence-integrity boundary for the Reforged world-item repository. A successful save writes the authoritative JSON state and an atomic SHA-256 sidecar. A protected load verifies the exact persisted bytes before deserialization, then applies the complete item and corpse validator.

## Safety behavior

Missing, malformed, or mismatched integrity metadata fails closed with `InvalidDataException`. Neither the save nor sidecar is silently repaired or replaced. Existing compatibility save/load methods remain available and unchanged.

## Evidence boundary

This is Reforged compatibility infrastructure. It does not claim that MajorMUD 1.11p used SHA-256 or this sidecar layout. Native checksum, journal, and cross-save coordination remain assigned to the final WCC/WBB/native pass.
