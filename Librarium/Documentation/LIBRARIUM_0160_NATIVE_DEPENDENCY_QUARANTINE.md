# CodeUpdate0160 — Native Dependency Quarantine and Promotion Contract

The managed Textblock, Command, and Ability runtime remains closed for the current decoded corpus. This pass converts the four remaining native-only unknowns into executable startup validation and machine-readable promotion gates. No native behavior is invented or activated.

## Quarantined dependencies

1. `native.message_payload_catalog` — 130 message references.
2. `native.textblock_payload_catalog` — 389 textblock invocations.
3. `native.remoteaction_dispatcher` — the observed `remoteaction 1423 66 0 3` call.
4. `native.player_command_table` — authoritative comparison boundary for the 77 managed registrations.

Each dependency now records blocked surfaces, required evidence, provenance requirements, fail-closed fallback, reference counts, and a concrete promotion test. Runtime startup rejects missing, duplicate, renamed, executable, or insufficiently evidenced dependency records.

## Runtime policy

Native-only data remains non-executable. Unresolved payload identifiers are never rendered to players. Managed downstream contracts remain authoritative until native evidence passes the matching promotion test. New native evidence extends only the associated dependency record and does not reopen already proven managed semantics.

## Next ownership

All remaining work transfers to the final WCC/WBB/native DLL exhaustion pass.
