# Authoritative Decoded Runtime Conversion

This revision changes the runtime ownership rule:

- Every generated WCC gameplay record is live world-scoped runtime authority.
- Every generated Worldgroup record required for gameplay is live authority.
- Provenance, confidence, pending-link, and quarantine labels remain diagnostic metadata only; they do not suppress structurally decoded gameplay records.
- All top-level generated JSON families are discovered, validated, retained in memory, and indexed by canonical and legacy identity.
- Generated commands are activated as runtime vocabulary.
- Generated status-effect and loot-rule records are no longer quarantined.

Worldgroup behavior is runtime authority only when an explicit MajorMUD dependency requires it for:

- gameplay or world-state mutation;
- MajorMUD module/session lifecycle dispatch;
- game scheduling, ticks, timers, cleanup, or resets;
- character/world persistence and recovery semantics; or
- Telnet/MegaMUD-facing protocol compatibility.

BBS-only infrastructure is excluded from the Reforged runtime. This includes bulletin boards, BBS mail/chat, menus, access accounting, modem/dial-up handling, unrelated door/module management, and Worldgroup host administration. BBS evidence may remain in Librarium as provenance, but it must not become a generated runtime requirement.

Every structurally decoded MajorMUD gameplay field requires a traceable compiler output, runtime loader, and live consumer. When exact native behavior is still unproven, the consumer must expose an evidence-gated failure instead of silently discarding the decoded value.

Action-gated exits use only their specifically bound generated room-interaction IDs. They must never count unrelated character or quest actions. Strongly correlated action promotions use the same executable interaction schema as directly decoded actions; exits whose required action set remains incomplete fail closed as evidence-only.

Classic runtime exclusions:

- `ai_profiles` is candidate tooling evidence and is not a Classic gameplay family. The app's AI remains outside server gameplay authority; monster behavior is driven by decoded NPC, spawn, ability, and world data.
- `guilds` is candidate evidence for a system not present in this Classic runtime and is not generated or loaded.
- `events` contains candidate DataSim-derived room-spawn and monster-behavior records whose mappings remain `pending_generator_export`. Those records stay in Librarium as evidence instead of creating a duplicate runtime authority; generated rooms, spawns, and NPCs own the decoded timing inputs, while live runtime services own event production and scheduling.
- `areas`, `assets`, `client_views`, `conflicts`, `evidence_records`, `regions`, `validation_rules`, and `world_overlays` currently contain catalog summaries, evidence/tooling metadata, UI projections, or Custom/AI policy rather than concrete Classic gameplay records. Their Librarium sources are preserved, but they are not emitted into `Data/Classic`.
