# Economy CodeUpdate0157 — Global Economic Persistence

## Implemented

This pass adds a single atomic persistence coordinator over the already implemented currency, commerce, banking, loot, valuation/service, merchant-lifecycle, and limited-item persistence surfaces. Participants retain ownership of their payload schemas; the coordinator owns aggregate capture, validation, checksums, replay protection, durable commit ordering, rollback, and recovery.

Supported save triggers are autosave, logout, disconnect, shutdown, administrative save, and recovery checkpoint. Each save captures every registered component, validates identity and schema versions, hashes every payload, creates a sequenced journal entry, creates a checkpoint, writes the envelope atomically, then commits the replay key. Any failure restores all participant states and coordinator state.

Recovery supports strict-current, last-known-good backup, and validated journal replay modes. Checkpoint, component, and journal SHA-256 integrity is verified before activation. Unsupported schemas, missing or duplicate components, checksum failures, and failed participant validation fail closed. Restore failure rolls all participants back to their pre-recovery snapshots.

## Ownership boundary

Subsystems own serialization and restoration of wallets, accounts, merchant stock, loot corpses, service state, merchant lifecycle, and limited-item ledgers. Economy global persistence owns coordination and durability only. Host lifecycle code owns when configured autosave/logout/disconnect hooks are invoked.

## Native dependency

Exact MajorMUD autosave cadence, logout/disconnect sequencing, corruption fallback, journal format, and crash-recovery ordering remain unresolved pending final WCC/WBB/native DLL exhaustion. No native constants or ordering were invented. The active fallback is deterministic configuration, atomic writes, bounded journaling, checksum validation, strict rejection by default, and explicitly selected backup or journal recovery.
