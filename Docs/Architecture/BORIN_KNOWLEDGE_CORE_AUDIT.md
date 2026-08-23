# Borin Knowledge Core architectural audit

Date: 2026-08-21

## Existing systems retained

- `BorinMemoryStore`, `BorinEpisodicMemoryStore`, and `BorinGameplayLearningStore` already provide durable memory, investigations, and verified gameplay observations. These remain separate from short-lived `BrainContext` working state.
- `BorinContextBuilder` already builds the single provider-neutral evidence package used by Local, NVIDIA Lightning, and NVIDIA Ultra. The two NVIDIA tiers do not own separate memories.
- `TieredBorinAiProviderRouter` already selects Local, Lightning, or Ultra without changing Borin identity or authoritative state.
- `BorinKnowledgeIndex` already maintains a persistent approved-text index at `AI/Memory/Knowledge`, uses content hashes, removes stale files, supports changed-file refresh, and returns bounded line-addressed evidence.
- `BorinReforgedKnowledgeBootstrap` already supplies compact subsystem routing cards and detects a stale repository fingerprint.
- `BorinReadOnlyAccess` already supplies current source evidence to engineering conversations. `BorinClassicTraceAccess` supplies decoded/imported/runtime provenance traces.
- The Librarium is the existing human-facing authority for imported and decoded MajorMUD records. Its schemas already retain relationships, evidence, conflicts, source policy, and runtime observations. Bundle 2 must adapt this authority; it must not copy the data into a competing game database.
- Reforged already has controlled editor/runtime services, Test Realm concepts, validation/issue records, build/test tooling, and the isolated Borin engineering workspace.
- `ForgeGuardianView` already contains a Reforged Knowledge Index section and commands for full and changed-file learning. It should be extended rather than replaced.

## Gaps found

- The active application had no Roslyn dependency or typed C# symbol/reference graph.
- Search was exact/lexical chunk ranking only; exact, semantic, and relationship retrieval were not explicit contracts.
- Authority strings existed, but permanent results did not expose a uniform confidence/provenance contract.
- Repository freshness was detected, but no debounced file watcher scheduled incremental refresh automatically.
- There is no active SQLite, embedding, or vector database. Adding one now would duplicate the proven JSON persistence model. Bundle 1 therefore uses deterministic conceptual-term expansion and Roslyn relationships for semantic retrieval, and labels that implementation accurately.
- Runtime observations, episodes, Librarium records, and source evidence are separate capable stores but lacked one query facade.

## Smallest clean architecture

`BorinKnowledgeCore` is the provider-neutral facade. It wraps the existing `BorinKnowledgeIndex`, adds a persistent Roslyn-derived C# symbol/reference graph, merges ranked evidence, attaches confidence and provenance, and schedules changed-file refreshes. Existing Forge and provider context paths consume it through the existing `IBorinKnowledgeIndex` contract, so Lightning and Ultra automatically receive the same facts.

The first bundle deliberately keeps storage split by authority:

- approved text/chunks: existing `BorinKnowledgeIndex` JSON;
- C# symbols/relationships: Knowledge Core Roslyn JSON;
- durable/episodic/runtime memory: existing Borin stores;
- MajorMUD records and relationships: existing Librarium/imported/decoded data (Bundle 2 adapter).

This is one logical knowledge service over authoritative stores, not one duplicated physical database.

## Confidence policy

- `PROVEN`: current source declaration/reference or authoritative generated/imported record with an exact location.
- `STRONGLY CORRELATED`: multiple consistent evidence relationships without complete runtime verification.
- `LIKELY`: supported inference with incomplete linkage.
- `HYPOTHESIS`: investigation lead only.
- `UNKNOWN`: insufficient evidence.

NVIDIA output is never itself promoted to a permanent fact. Current local source and data evidence remains authoritative.

## Bundle 3 completion

Bundle 3 extends the same logical Knowledge Core without replacing Borin's conversational episodic memory or copying Librarium records:

- Runtime observations retain server, world, character, room, timestamp, evidence path, and optional canonical MajorMUD/Librarium entity IDs.
- Repeated expected-versus-observed mismatches become searchable conflict records only after the configured controlled-attempt threshold. Resolution preserves both original claims and requires verification evidence.
- Evidence-backed investigation episodes are stored separately from user-conversation episodes. They are explicitly marked `Verified` or `Unverified` and retain related canonical data/source identifiers.
- Procedures cannot enter trusted procedural memory without a successful verification result and a nonempty evidence citation.
- A successful isolated Borin engineering build and test records a verified investigation and promotes the proven approval-gated repair workflow. A failed build/test teaches no trusted procedure and never modifies the main repository.
- Exact, semantic, and relationship retrieval merge these observations, conflicts, investigations, and procedures with current Reforged and MajorMUD authority for both Lightning and Ultra.
- The Forge Knowledge panel reports observations, investigations, conflicts, and verified procedures and provides views for conflicts and investigations.
- The evidence-based mastery suite exercises authoritative identity retrieval, typed mechanics, relationships, navigation, provenance/confidence, and the verified-learning lifecycle. Its score is calculated only from executed gates.

Librarium integration is deliberately by canonical reference: runtime learning points to authoritative IDs such as `room.map1.472`, while Librarium/imported/generated data remains the fact owner. Divergent runtime evidence creates a conflict instead of overwriting that authority.
