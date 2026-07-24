# Items Runtime Semantic Exhaustion — CodeUpdate0193

## Executable advancement

CodeUpdate0193 combines validated backup generations with byte-level integrity verification. Primary and backup saves have independent SHA-256 records. Recovery accepts a backup only after hash verification, deserialization, and complete item/corpse validation, then restores both authoritative data and its sidecar.

## Safety behavior

An invalid primary is never rotated into backup. A missing or mismatched backup sidecar fails closed. When neither generation validates, persisted files remain untouched and recovery reports the primary and backup failures.

## Evidence boundary

This remains Reforged compatibility infrastructure. Native MajorMUD checksum algorithms, backup generation counts, journaling, and cross-subsystem commit ordering remain assigned to the final native pass.
