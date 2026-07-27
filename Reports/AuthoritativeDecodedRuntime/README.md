# Authoritative Decoded Runtime Conversion

This revision changes the runtime ownership rule:

- Every generated WCC gameplay record is live world-scoped runtime authority.
- Every generated Worldgroup record required for gameplay is live authority.
- Provenance, confidence, pending-link, and quarantine labels remain diagnostic metadata only; they do not suppress structurally decoded gameplay records.
- All top-level generated JSON families are discovered, validated, retained in memory, and indexed by canonical and legacy identity.
- Generated commands are activated as runtime vocabulary.
- Generated status-effect and loot-rule records are no longer quarantined.

Worldgroup host/BBS infrastructure remains outside gameplay authority unless a generated gameplay family depends on it.
