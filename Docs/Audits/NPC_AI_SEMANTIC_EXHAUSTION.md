# NPC and AI Semantic Exhaustion

## CodeUpdate0151 — world-owned deterministic scheduling

Executable runtime additions:

- deterministic NPC processing ordered by runtime instance ID;
- world-owned explicit combat target state;
- world-owned queued merchant/trainer interaction state;
- evidence-constrained decision scheduling through the CodeUpdate0150 decision engine;
- persisted per-instance `last_ai_tick`, `last_ai_decision`, and `last_ai_evidence` metadata;
- one-decision consumption of supported service interaction requests;
- replay smoke coverage for scheduling order, attack handoff, service handoff, and deterministic results.

Ownership promoted in Librarium:

- definitions are generated world-definition data;
- live NPC instances, room/home room, respawn timer, explicit target, and queued interaction are owned by `RuntimeLivingWorldService`;
- full candidate traces are world-tick results, while the latest selected decision summary is persisted with the instance.

The pass does not infer native perception, threat/hate, assist, flee, patrol/pathing, or spell/ability priority. Those branches remain blocked and resolve to the safe hold-position decision unless an explicit evidence-backed request exists.


## CodeUpdate0152 — monotonic tick admission and lifecycle cleanup

Executable runtime additions:

- persisted `last_ai_tick` is now an admission watermark rather than trace-only metadata;
- duplicate and regressive world ticks resolve to `AlreadyProcessed` without scheduling another action;
- dead, despawned, and waiting-to-respawn decisions clear transient target and service-request state;
- dead-state cleanup marks combat hostility as `dead`;
- safe idle fallback normalizes combat hostility to `idle`;
- empty cleanup values remove metadata keys instead of persisting ambiguous empty state;
- replay tests cover persisted duplicate suppression and death-state cleanup scheduling.

This pass does not assign original native tick cadence, threat memory, corpse ordering, or respawn retry semantics. It makes the Reforged scheduler save-safe and idempotent while preserving those branches as explicit native dependencies.


## CodeUpdate0153 — deterministic action handoff and acknowledgement

Executable runtime additions:

- actionable NPC decisions now emit stable `RuntimeNpcActionIntent` records;
- action IDs are derived from NPC instance, admitted world tick, and selected action kind;
- basic-attack intents carry only the explicit runtime target already admitted by the evidence-constrained decision engine;
- merchant/trainer service intents carry the queued request and consume the source request once committed;
- pending action identity and payload are persisted on the world-owned NPC instance;
- `AcknowledgeNpcAction` clears a pending action only when the caller supplies the exact persisted action ID;
- duplicate or regressive ticks emit no action intent, preventing replay from scheduling duplicate side effects.

This pass does not execute combat formulas, economy transactions, trainer progression, perception, threat, or native action priority. It creates a deterministic, save-safe boundary for those owning subsystems to consume without allowing NPC AI to invent their semantics.


## CodeUpdate0154 — pending-action lifecycle barrier and completion outcomes

Executable runtime additions:

- a persisted pending action now has admission precedence over attack, service, or fallback selection;
- NPCs with unresolved actions return `AwaitingActionCompletion` and emit no replacement intent;
- `GetPendingNpcActions` reconstructs persisted action payloads in stable NPC-instance order after save/load;
- `CompleteNpcAction` requires the exact pending action ID, records the owning subsystem's outcome, and atomically clears the pending payload;
- mismatched or stale completion attempts fail without changing world state;
- latest completed action ID and outcome remain world-owned audit metadata.

The pass does not invent action results, retry cadence, combat resolution, merchant transactions, or trainer progression. Those results remain owned by their established execution subsystems; the NPC/AI runtime now supplies a deterministic, non-overwriting, save-safe handoff lifecycle.


## CodeUpdate0155 — exclusive action-consumer ownership

Executable runtime additions:

- persisted pending actions can be claimed by one named execution subsystem through `ClaimNpcAction`;
- claim IDs are deterministic functions of the persisted action ID and normalized consumer ID;
- a second consumer cannot claim an already claimed action;
- `GetClaimedNpcActions` reconstructs active claims in stable NPC-instance order after save/load;
- exact action and claim IDs are required to release or complete a claim;
- legacy unclaimed completion and acknowledgement paths reject actively claimed actions;
- delivery count survives release/reclaim cycles and is cleared only when the pending action is completed or acknowledged.

This pass does not assign claim expiration, retry timing, consumer priority, combat outcomes, shop transactions, or trainer progression. Those remain owned by their execution subsystems or explicitly blocked native dependencies.


## CodeUpdate0156 — explicit-policy claim lease recovery

Executable runtime additions:

- action claims persist the world tick at which ownership was acquired;
- exact claim holders can renew that watermark, while regressive renewals are rejected;
- the world runtime can recover abandoned claims using an explicit host-supplied maximum age in ticks;
- recovery processes NPC instances in stable ordinal order and preserves delivery count for the next claimant;
- claims with missing, malformed, or future acquisition ticks fail closed and are never automatically released.

This pass does not assert MajorMUD's native claim timeout or retry cadence. Recovery timing remains an explicit Reforged host policy until native evidence establishes the original value.


## CodeUpdate0157 — failed-action redelivery and quarantine

Executable runtime additions:

- exact claim holders can record a failed action attempt with a failure reason and world tick;
- a host-supplied maximum delivery count determines whether the pending action is released for deterministic redelivery or quarantined;
- quarantined actions retain the original pending intent and delivery count but reject new claims;
- quarantined actions are reconstructed in stable NPC-instance order after save/load;
- an exact pending action ID is required to release quarantine for another delivery attempt;
- completion and acknowledgement clear failure and quarantine metadata atomically.

This pass does not assert MajorMUD's native retry count, terminal-failure behavior, or retry cadence. Those remain explicit native dependencies; Reforged host policy controls the safety boundary without discarding pending world state.

## CodeUpdate0158 — Spawn-slot and respawn lifecycle ownership

Activated a single world-runtime owner for materialized NPC spawn slots and due respawn admission. Generated `spawn_id`, canonical home room, and `respawn_minutes` now drive a deterministic waiting-to-respawn transition and stable due ordering. Respawn restores canonical home-room placement and maximum HP, increments a persisted spawn generation, and clears stale combat targets, interaction requests, pending actions, consumer claims, delivery/failure state, and quarantine state.

The lifecycle fails closed when the canonical home room is unavailable. It does not select an alternate room or infer native population behavior. Spawn chance, dynamic monster-group rolls, population maxima beyond the materialized projection, native retry cadence, and hard-reset exceptions remain explicit native dependencies.


## CodeUpdate0159 — Atomic Death and Blocked Respawn Recovery

Terminal combat damage now enters the same locked lifecycle transition as explicit despawn, so AI action metadata, hostility, death evidence, timer ownership, and persistence are updated atomically. A due respawn with an unresolved canonical home room is converted into a persisted blocked state and emits a single audit transition. The runtime does not invent a retry cadence; `RetryBlockedNpcRespawn` requires an exact instance and a resolved home room before making the transition due again.

Native failed-respawn cadence and hard-reset exceptions remain explicitly unresolved.


## CodeUpdate0160 — Explicit Spawn-Slot Control and Respawn Suppression

Added an exact, persisted administrative boundary for materialized spawn slots. Host policy can enable or disable a canonical generated `spawn_id` without inventing Classic population rules. Disabling a slot leaves living NPCs untouched, but dead members enter a persisted `respawn_suppressed` state and cannot be admitted by lifecycle ticks. Re-enabling the same exact slot clears suppression and schedules its dead members for deterministic immediate admission through the existing home-room validation boundary.

This pass does not activate spawn chance, native monster-group selection, population maxima, or hard-reset exceptions. Those remain explicit native dependencies.

## CodeUpdate0161 — Explicit Spawn-Slot Reset Boundary

The world runtime now exposes an exact administrative reset for a canonical materialized spawn slot. The operation is serialized under the world lock, clears stale NPC execution state, routes living members through the established death admission boundary, and schedules dead members for deterministic immediate re-admission. Disabled slots remain suppressed; the operation never silently changes host spawn-slot policy.

This is a Reforged administrative lifecycle boundary, not a claim about the original native soft-reset or hard-reset algorithm. Native reset exceptions, population rolls, and chance behavior remain explicitly unresolved.


## CodeUpdate0162 — Explicit Perception and Target-Acquisition Ownership

Added an executable observation boundary without assigning unresolved native meaning. A host subsystem may submit a world-tick perception snapshot for one materialized NPC. The runtime validates snapshot ownership and room identity, rejects dead, self, cross-room, non-detectable, and non-hostile candidates, retains a still-valid prior target, and otherwise selects the first admitted entity in stable ordinal order.

The selected target and compact perception audit summary persist with the NPC world state. No sight range, hearing range, stealth roll, faction rule, threat score, or target-memory duration is inferred. With no explicitly detectable hostile same-room candidate, the target is cleared and AI safely falls back to its existing hold behavior.


## CodeUpdate0163 — Explicit Threat Ranking Ownership

Added an executable threat-selection boundary while preserving Combat ownership of threat generation. A host subsystem may submit one world-tick threat snapshot for a materialized NPC. The runtime validates NPC and room ownership, rejects self, dead, cross-room, non-detectable, non-hostile, and negative-score candidates, then selects the highest explicit threat with stable ordinal entity-ID tie-breaking.

The selected target, explicit score, authority, tick, and compact candidate/rejection counts persist with world-owned NPC state. This pass does not infer native damage threat, healing aggro, spell aggro, taunt, decay, assist propagation, memory duration, or chase behavior. With no admitted candidate, the target is cleared and the existing safe decision fallback remains active.


## CodeUpdate0164 — Explicit Assist Admission

Added a deterministic host-supplied assist boundary. Same-room, alive, detectable caller and target evidence is validated before an explicitly hostile target may replace the NPC target. Rejected assist requests preserve the prior target. Native assist radius, factions, propagation, hate transfer, and cooldown remain blocked.


## CodeUpdate0165 — Explicit Flee Admission

Added a deterministic host-supplied flee boundary. A living same-room NPC may enter a persisted fleeing state only when the host explicitly requests flee and supplies an already validated reachable destination room. Acceptance clears the stale combat target and records the destination and compact audit metadata. Rejection leaves room and target state unchanged. Native HP thresholds, morale, destination selection, chase/leash behavior, movement costs, interruption, cooldowns, and retry cadence remain blocked.


## CodeUpdate0166 — Exact Admitted Flee Movement Completion

Added a save-safe movement commit boundary for NPCs already admitted into fleeing state. The owning movement subsystem must report completion for the exact persisted flee destination, the source room must still match the NPC's current world-owned room, and the destination must resolve to a generated runtime room. Only then does the world runtime mutate `RoomId`, clear fleeing and stale target metadata, and persist compact completion evidence.

Rejected or contradictory completions do not move the NPC and do not discard the admitted flee state, allowing an explicit corrected retry. This pass does not infer exit selection, route finding, movement cost, restrictions, interruption, chase, leash, cooldown, or retry cadence.


## CodeUpdate0167 — Explicit Return-Home Admission and Exact Completion

Activated an auditable return-home boundary without inferring native chase or leash rules. A living NPC may enter returning-home state only from an explicit host request that agrees with current-room ownership, persisted canonical home-room ownership, and explicit reachability. The current target is cleared only after admission. Room mutation occurs only when the movement subsystem confirms traversal to the exact admitted canonical home room. Rejected completions preserve the admitted state for exact retry.

Still native-blocked: leash distance, chase termination, route/exit selection, restrictions, movement cost, interruption, and retry cadence.


## CodeUpdate0168 — Explicit Threat Generation Ledger

Added a world-owned threat-generation boundary that accepts only host-supplied positive deltas for alive, detectable, explicitly hostile same-room targets. Deltas accumulate with checked arithmetic into a persisted per-NPC ledger, duplicate or regressive world ticks are rejected without mutation, and the highest accumulated threat selects the target with stable ordinal tie-breaking. Native damage, healing, spell, taunt, decay, hate-transfer, and memory formulas remain blocked.


## CodeUpdate0169 — Explicit Threat Reduction Boundary

Activated an explicit host-owned threat-reduction boundary over the persisted CodeUpdate0168 ledger. Positive reductions are applied in stable event-ID order with zero-flooring, optional removal at zero, monotonic tick replay suppression, and deterministic target reranking. Unknown ledger targets and invalid reductions are rejected without mutation. Native decay cadence, percentage formulas, memory duration, taunt and hate-transfer remain unresolved.


## CodeUpdate0170 — Explicit Threat-Memory Removal Boundary

Activated an exact host-authorized threat-memory boundary over the persisted NPC threat ledger. A target is removed only when the host supplies an explicit forget authorization for that exact entity. Duplicate or regressive world ticks are rejected, unauthorized requests preserve the ledger, and remaining targets are reranked deterministically with stable entity-ID tie-breaking.

This pass does not assign MajorMUD's native target-memory duration, lost-target timeout, visibility-driven forgetting, or combat disengagement behavior. Those remain explicit native dependencies.


## CodeUpdate0171 — Explicit Taunt Admission Boundary

Activated an exact host-authorized taunt boundary over the persisted NPC threat ledger. A taunt may override the current target only when the host explicitly authorizes the event and supplies a same-room, living, detectable, explicitly hostile target plus a nonnegative threat floor. The runtime applies the floor without reducing existing threat, resolves multiple admitted events in stable event-ID order, records the last admitted exact target, and suppresses duplicate or regressive world ticks.

This pass does not assign native taunt strength, duration, resistance, immunity, cooldown, hate-transfer, or forced-target break conditions. Those remain explicit native dependencies.


## CodeUpdate0172 — Native-independent runtime inspection

Added deterministic read-only inspection by instance, NPC, spawn, and room. Snapshots expose lifecycle, health, target, threat ledger, action claim, respawn, flee, and return-home state without changing Classic behavior. Unknown instances fail safely and malformed threat metadata is not rewritten.

## CodeUpdate0173 — Native-independent administrative controls

Implemented host-owned AI enable/disable, freeze/unfreeze, and exact canonical room movement. Disabled AI clears pending execution state and cannot emit decisions. Frozen NPCs remain materialized but cannot emit decisions or retain in-flight flee/return-home movement state. Administrative movement rejects missing instances, dead instances, and noncanonical destinations without mutation. No native Classic AI semantics were inferred.

## CodeUpdate0174 — Generated NPC Defensive Profiles

Activated the existing definition-to-combat defensive projection as an explicit NPC/AI runtime boundary. Every canonical NPC definition now has a validated defensive profile carrying generated armor class, damage resistance, and magic resistance. The projection intentionally supplies zero generic defense and zero dodge because the generated NPC schema exposes no separate NPC dodge field and the retained simulator dodge input is player-side. Invalid NPC IDs or nonpositive hit points fail closed. No native DLL behavior was inferred.

## CodeUpdate0175 — Exact NPC Dialogue Admission and Completion Ownership

Integrated the already generated quest-dialogue executor with the world-owned NPC runtime. The `talk` command must now acquire an exact per-instance dialogue request before textblock or quest execution begins. Admission validates instance existence, life state, actor identity, and current-room ownership, and prevents concurrent dialogue ownership. Completion must match the exact persisted request ID and records matched, unmatched, or dispatch-exception outcomes before releasing the instance.

This pass does not infer greeting cadence, idle speech, random speech, combat/death dialogue triggers, cooldowns, visibility masks, or native conversation locking. Those remain separate evidence or native-dependent work.

## CodeUpdate0176 — Merchant and trainer service ownership
Generated merchant and trainer transactions now require admission through a living same-room NPC instance whose generated interaction model supports the requested service. A single exact request owns execution until success or failure completion. Character, item, shop-stock, economy, and trainer formulas remain in their established subsystems. Native restock cadence, shop hours, refusal dialogue, and generated stock mutation rules not already present remain unresolved rather than inferred.


## CodeUpdate0177 — NPC inventory ownership and exact transfer boundary

- Added stable inspection of items owned by a living NPC instance.
- Added same-room, alive-instance admission for exact character-to-NPC item transfer.
- Added explicit-authority return and current-room drop operations for NPC-owned items.
- Preserved spawn inventory, equipment selection, item-use AI, ammunition, loot generation, and corpse transfer as separate unresolved boundaries.

## CodeUpdate0178 — NPC corpse inventory ownership

Implemented an exact, non-generative death-inventory bridge. A dead NPC's already-owned runtime items can be atomically transferred to the exact corpse identity and room supplied by the living-world lifecycle. Admission requires explicit authority, a dead instance, exact room ownership, a unique corpse ID, and nonnegative explicit gold. Living NPCs, duplicate corpses, invalid rooms, and missing authority fail without mutation. Loot generation, drop probability, corpse access, and native decay remain unresolved rather than inferred.


## CodeUpdate0179 — Explicit NPC Corpse Access and Cleanup

Added stable corpse inventory inspection, exact host-authorized item and gold looting, same-room ownership validation, and exact cleanup only after the already supplied corpse due timestamp. This pass does not infer native loot rights, party access, no-drop filtering, or decay duration.


## CodeUpdate0180 — Explicit NPC Spawn Inventory Admission

Added an exact host-owned boundary that creates canonical items directly under a living NPC instance from an explicit request manifest. Admission requires authority, a stable request ID, canonical item IDs, and valid quantities. Applied request IDs are retained in item metadata so replay does not duplicate inventory. Partial creation failures are rolled back. Native loadout selection, equipment decisions, drop probability, and respawn replenishment remain unresolved.

## CodeUpdate0181 — Exact NPC Equipment Ownership

- Added an exact host-authorized NPC equipment boundary for already NPC-owned item instances.
- Enforced living NPC ownership, explicit authority, generated equipment relationships, exact legal slots, slot occupancy, and existing two-handed conflicts.
- Added deterministic NPC equipment inspection plus exact equip and unequip operations.
- Preserved equipment-selection AI, NPC class/race eligibility, spawn equipment manifests, consumable use, ammunition use, and switching priorities as unresolved independent boundaries.


## CodeUpdate0182 — Exact NPC item-use ownership

Implemented a fail-closed host-authorized boundary for consuming an already NPC-owned carried or equipped usable item. The boundary does not select items or targets and does not infer effects, timing, cooldowns, or ammunition semantics.


## CodeUpdate0183 — Exact NPC ammunition ownership and consumption

Implemented a fail-closed ammunition boundary for requests already resolved by an owning combat or equipment subsystem. The request must identify a living NPC, an exact equipped weapon owned by that NPC, an exact carried ammunition stack owned by that NPC, a positive quantity, explicit compatibility confirmation, and nonblank authority. Accepted requests consume exactly the supplied quantity; all rejected requests preserve item state. No ammunition classification, weapon compatibility table, selection AI, rate of fire, attack timing, hit, or damage behavior is inferred.


## CodeUpdate0184 — Exact NPC Item-Effect Admission and Completion

Added a save-safe two-phase boundary between NPC item ownership and the subsystem that owns item effects. Admission requires a living NPC, an exact NPC-owned carried or equipped item, a nonblank target, and explicit authority. Only one request may own the NPC at a time. Completion must match the exact persisted request ID. An unsuccessful effect preserves item state; a successful exact completion consumes the item through `RuntimeItemEngine.UseNpc`. No effect behavior, target selection, magnitude, priority, cooldown, or native use ordering is inferred.


## CodeUpdate0185 — Exact NPC Equipment-Selection Handoff

Added a save-safe two-phase boundary between equipment-policy ownership and exact NPC equipment mutation. Admission requires a living NPC, an exact already-carried NPC-owned item, an exact generated slot, and explicit authority. Completion must match the exact request ID and equips only through the established equipment ownership service. No ranking, stat comparison, switching cadence, or native combat priority is inferred.

## CodeUpdate0186 — Exact NPC corpse loot materialization
- Added exact host-authorized canonical item materialization into an existing dead-NPC corpse.
- Added corpse-owner validation, request replay suppression, atomic rollback, evidence, provenance, validation, and Librarium ownership.
- Preserved loot selection, probability, guaranteed drops, quest/no-drop filtering, and native ordering as unresolved.

## CodeUpdate0187 — NPC Merchant Stock Ownership

Added an exact ownership bridge from a living merchant NPC to the existing persistent shop-state store. The bridge exposes stable stock inspection and exact host-authorized quantity mutation after validating the NPC, shop service, canonical item, authority, and quantity. Restock cadence, restrictions, hours, pricing, merchant gold, and native merchant AI remain unresolved rather than inferred.


## CodeUpdate0188 — NPC Trainer Offering Persistence Ownership

Added an exact ownership bridge from a living trainer NPC to persistent trainer-offering state. The bridge exposes stable offering inspection and exact host-authorized enable/disable plus next-availability mutation after validating the NPC, trainer service, offering identity, kind, and authority. Eligibility, cost, cooldown duration, progression, and refusal dialogue remain owned by their established subsystems or unresolved evidence; no native policy was inferred.


## CodeUpdate0189 — Exact trainer offering availability and cooldown commit

Added a fail-closed bridge between persistent trainer-offering state and the established NPC trainer-service admission lifecycle. An enabled offering is admitted only when the explicit request time reaches its persisted next-availability timestamp. Completion must match the exact active trainer-service request. Successful completion commits only the explicitly supplied next-availability timestamp; failed completion leaves the prior cooldown unchanged. Eligibility, costs, progression, cooldown duration, and refusal dialogue remain unresolved or owned elsewhere and are not inferred.

## CodeUpdate0190 — Exact merchant access admission and completion

- Added an exact host-resolved admission boundary for NPC-owned buy, sell, and repair interactions.
- Requires explicit shop-open, actor-eligibility, and currency-authorization decisions before entering the existing living same-room service owner.
- Exact request-ID completion prevents stale or mismatched merchant completions.
- Pricing, stock changes, item transfer, currency transfer, shop-hour derivation, and native restrictions remain under their existing owners or explicitly unresolved.


## CodeUpdate0191 — Exact merchant restock admission

Implemented a replay-safe, atomic merchant restock boundary. The host policy supplies the exact canonical items, quantities, next-restock timestamps, request ID, and authority. The NPC runtime validates living merchant ownership and shop identity, then persists the exact stock batch and applied request ID. Native cadence, stock generation, limits, and scheduling remain unresolved and inactive.

## CodeUpdate0192 — Exact trainer eligibility admission

Added a fail-closed bridge from character/progression policy into the established persistent trainer offering admission lifecycle. The host must explicitly authorize actor eligibility, required resources, and progression before the trainer request can be admitted. The NPC runtime does not derive class, race, level, cost, training-point, promotion, or refusal-dialogue rules.

## CodeUpdate0195
Added exact companion combat-assistance ownership and fail-closed target admission.


## CodeUpdate0196 — Exact NPC companion lifecycle cleanup

Added an exact owner-authorized cleanup boundary for dismissal, owner logout, owner disconnect, explicit duration expiry, charm break, and already-dead companion cleanup. Accepted cleanup removes companion ownership, pending follow state, companion assist metadata, current target, and persisted threat ledger while retaining compact audit metadata. Native event detection, duration, despawn, corpse, save deletion, and charm-break policy remain unresolved.

## CodeUpdate0197 — Exact NPC companion command boundary

Added exact owner-authorized stay, follow, assist, passive, and dismiss command admission. The runtime validates living companion ownership and authority, persists command mode and audit metadata, and only clears already-owned follow/combat/ownership state. Native parsing, routing, target selection, cadence, and formulas remain unresolved.

## CodeUpdate0198 — Exact NPC multi-step route boundary

Added exact host-supplied canonical route admission, per-step traversal completion, repeat progression, and cancellation. Route selection, pathfinding, exit legality, movement costs, cadence, chase/leash policy, and native movement rules remain unresolved.

## CodeUpdate0199 — NPC Persistence Integrity Validation

Activated a deterministic world-owned validation boundary over every materialized NPC save record. The validator reports unresolved current/home rooms, HP and life-state inconsistencies, missing dead-NPC respawn ownership, incomplete pending-action claims, malformed explicit threat ledgers, and invalid explicit-route state. Under an explicit nonblank repair authority it may remove only already-proven orphaned action-claim metadata and dead-NPC target/flee/return-home transient fields, recording authority, timestamp, and world tick before committing one existing world save. It does not invent rooms, HP, respawn timing, threat values, route steps, or native lifecycle policy.

## CodeUpdate0200 — Exact NPC gold ownership

Added deterministic world-owned NPC gold inspection plus exact authority-gated set and delta mutation. Negative balances, underflow, overflow, malformed persisted values, missing instances, and missing authority fail closed. Spawn gold, merchant funding, pricing, rewards, loot allocation, denominations, and native limits remain unresolved.

## CodeUpdate0201 — Exact NPC merchant gold settlement
An already-admitted buy, sell, or repair request may settle an exact host-supplied positive amount between actor carried currency and persisted NPC gold. Exact actor, request, service-kind, authority, and balance checks are required; failures restore both balances. Native pricing and merchant-funding policy remain unresolved.

## CodeUpdate0203
Exact administrative transient interaction cancellation is executable and save-audited.
