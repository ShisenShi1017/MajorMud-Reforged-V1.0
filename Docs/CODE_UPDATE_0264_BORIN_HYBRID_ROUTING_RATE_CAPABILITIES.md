# Code Update 0264 — Borin Hybrid Routing, Shared Rate Budget, and Role Capabilities

## Outcome

Borin now has the provider-routing and capability foundation needed to remain local-first while using NVIDIA for complex reasoning.

## Hybrid routing

`HybridAiProviderRouter` supports:

- Hybrid — routine gameplay and ordinary turns remain local; complex diagnosis, engineering, Sysop, architecture, repair, and ambiguous analysis use NVIDIA.
- Local Only — NVIDIA is never called.
- NVIDIA Only — NVIDIA is preferred, subject to the configured safe local fallback.

Classified NVIDIA transport/provider failures fall back to the local provider without losing Borin's local context, memory, personality, session, or objective. Caller cancellation is never swallowed or converted into fallback.

The router is not yet registered as the active Forge brain. UI configuration and persisted settings remain the activation boundary.

## Shared NVIDIA budget

`SlidingWindowAiRateBudget` is designed for one shared registration per NVIDIA credential:

- normal target: 20 requests per minute
- hard ceiling: 40 requests per minute
- optional interactive bursts may consume capacity up to the hard ceiling
- bounded queue wait
- cancellation while queued
- sliding-window accounting
- status snapshot with usage, remaining hard requests, waiters, and reset time

`RateLimitedAiProvider` applies the shared budget without coupling the NVIDIA transport to a single Borin role.

## Complete Borin capability catalog

`BorinCapabilityCatalog` inventories the controlled local capability surface for the same Borin identity.

### Player

Connection/login, disconnect, look, movement, inventory, equipment, attack, cast, interaction, shops, quests, and pathfinding through the canonical session and normal game-command gateways.

### Sysop

World monitoring; room, exit, monster, item, script, trigger, and runtime inspection; issue reporting; Test Realm proposal, approved application, and reset capabilities.

### Developer

Repository search/read/directory/reference tools; build, runtime, crash, and test evidence; propose/preview/apply/revert isolated patches; builds and tests; focused tests; test-server lifecycle; runtime inspection; before/after comparison; issue lifecycle; repair reports.

Each capability records its local authority. Role and permission filtering occurs before capabilities enter provider context. Test Realm mutations are labeled and approval-gated. Production mutation is not exposed.

The catalog grants no service reference or execution ability to a model. Tool adapters remain responsible for argument validation and delegation to the existing Borin, Forge, Editor, Guardian Hall, Host, and runtime services.

## Files

- `Apps/Source/Reforged.App/Brains/BorinAiRouting.cs`
- `Apps/Source/Reforged.App/Brains/BorinCapabilityCatalog.cs`
- `Apps/Source/Reforged.App/Brains/BorinContextBuilder.cs`
- `Apps/Source/Reforged.App/Brains/BorinStructuredActions.cs`
- `Apps/Source/Reforged.App/Brains/AiProviderContracts.cs`
- `Apps/Source/Borin.AiProviderTests/Program.cs`

## Verification

- Provider/context/action/routing/rate/capability tests: 21/21 pass.
- Existing Borin character regression and full Release solution build are the final gates for this bundle.

No live NVIDIA request, model-driven command, file edit, world change, or production operation occurred.
