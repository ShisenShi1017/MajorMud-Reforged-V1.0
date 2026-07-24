# Items Runtime Semantic Exhaustion — CodeUpdate0191

## Executable advance

CodeUpdate0191 adds validated backup-generation rotation and deterministic world-item recovery. The Items repository now refuses to rotate an invalid primary into backup, validates a backup independently after primary failure, and atomically restores the validated backup to the authoritative primary path before returning recovered state.

## Evidence boundary

This is Reforged compatibility and crash-recovery infrastructure. It does not claim the `.bak` suffix, generation count, or recovery order matches native MajorMUD. No native journal layout or cross-subsystem atomicity has been invented.

## Runtime behavior

- `SaveValidatedWithBackup` validates both the new state and the existing authoritative generation before rotation.
- `LoadValidatedWithRecovery` prefers a valid primary.
- A missing, corrupt, or validator-rejected primary may fall back only to a separately validated backup.
- Successful fallback atomically reconstructs the primary file.
- Failure of both generations throws and preserves evidence.

## Validation

Deterministic smoke coverage verifies generation rotation, primary corruption, backup selection, generation identity, primary reconstruction, and a subsequent ordinary validated load.

## Remaining native dependencies

Native serialization layout, journal files, generation retention, crash checkpoint coordination, and cross-save recovery ordering remain for the final WCC/WBB/native pass.
