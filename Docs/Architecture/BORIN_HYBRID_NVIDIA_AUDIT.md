# Borin Hybrid NVIDIA Architecture Audit

Date: 2026-08-20

## Audit conclusion

Borin is already a substantial, integrated local system. NVIDIA must be added as an optional provider beneath Borin's existing brain/context boundary. It must not replace the local GGUF brain, local memory stores, player controller, canonical client session, Forge developer tools, Guardian Hall policies, or server/editor authority.

The minimum safe change is:

1. Keep `BrainContext` as the locally assembled authority package.
2. Add a provider-neutral `IAiProvider` layer with local and NVIDIA implementations.
3. Add a later hybrid router that selects a provider by objective/complexity and falls back to local AI.
4. Feed provider output into the existing Borin decision/action gateway, never directly into the terminal or filesystem.
5. Preserve one `ClientSessionConnection` for both Play and Forge views.

Phase 2 and the provider portion of Phase 3 begin in this bundle. Provider activation, hybrid routing, rate scheduling, structured actions, and UI selection remain deliberately unconnected until their validation layers exist.

## Existing end-to-end path

### Network and terminal session

- `ClientSessionService` owns the real TCP/telnet connection, negotiation, receive loop, outgoing command path, logging, parsing, and the `ClientSessionConnection` collection.
- `ClientSessionConnection` contains connection state, terminal lines/entries, parsed prompt, room, exits, HP/mana, inventory/equipment, timestamps, command ownership, and terminal presentation state.
- `PlayViewModel` operates those service-owned session objects.
- `ClientTerminalWindowContext` and `ClientTerminalWindow` bind to the selected existing `ClientSessionConnection`; they do not create a second network connection.
- `BorinPlayerResearch` resolves Borin's configured session ID from the same `IClientSessionService.Sessions` collection, attaches to an existing matching session when available, and subscribes to the same line/state/command events.

Therefore the intended shared-session design already exists at the object level:

`ClientSessionService -> ClientSessionConnection <- Play terminal / Forge Borin player controller`

The remaining session work is to formalize/version this state for stale-response rejection, not to create another connection type.

### Authentication detection and synchronization

- `BorinPlayerResearch.ConnectAsync` attaches to or creates the configured canonical client session and delegates the network connection to `IClientSessionService`.
- Its login monitor observes both line events and the canonical terminal screen buffer, covering prompts that arrive without CR/LF or before Borin attaches.
- `HandleLoginLineAsync` recognizes the Omniverse user ID, password, main menu, MajorMUD menu, and the in-game HP prompt.
- The password is read from `WindowsBorinSecureCredentialStore` and sent through the sensitive-command path.
- Recognition of the game prompt sets `BorinLoginState.Ready`.
- `BorinGameStateSnapshot.Capture` combines that login state with the current `ClientSessionConnection`; it requires connection, Ready state, and a fresh verified prompt before reporting Borin in game.
- `BrainContext.ForBorin` carries the resulting `BorinGameStateSnapshot` to the model.

This is an architectural state-flow repair already present in current code, not a prompt-only claim. Existing Borin character tests cover canonical prompt and truthful connection behavior. A future state-version field is still required before cloud-proposed gameplay actions can execute autonomously.

### Parsing and game-state tracking

- `ClientSessionService` feeds specialized local parsers for prompt, room display, gameplay lines, inventory/equipment, statistics, spellbook, party, who, Librarium pages, and boss/home information.
- Parsed values and freshness timestamps live on the canonical `ClientSessionConnection`.
- `BorinGameStateAwareness` converts that data into an evidence-bounded snapshot and explicitly clears stale retained parser values after disconnect.
- `BorinGameplayLearning`, player journal, mastery curriculum, and local memory stores consume observations locally.

Raw terminal output is therefore not the correct NVIDIA context source. The verified snapshots are.

### Local brain, personality, and memory

- `IBrain` is the existing provider-neutral user-facing brain contract.
- `LocalGgufBrain` is the active Borin brain and uses LLamaSharp/Vulkan directly.
- `BrainContext`/`ConversationContext` already aggregate personality/configuration, repository snapshot, durable memory, read-only evidence, Classic trace, knowledge index/bootstrap, living-character state, conversation direction, social and episodic memory, consolidation, temporal continuity, character growth, initiative, voice, repair state, situational/group/game awareness, gameplay learning, and self-knowledge.
- Persistent memory is split across `BorinMemory`, `BorinSocialMemory`, `BorinEpisodicMemory`, `BorinMemoryConsolidation`, `BorinGameplayLearning`, `BorinPlayerJournal`, knowledge indexes, and engineering memory/workspace records under local `AI` state.
- `LocalGgufBrain` performs relevance preparation and prompt-budget composition rather than sending every stored memory.
- Borin's dwarf identity is stored in `ForgeConfiguration` and composed independently of the model implementation.

### AI model/provider architecture

- `IBrain` and `BrainResponse` already prevent the Forge UI from depending directly on LLamaSharp.
- `LocalGgufBrain` is the active implementation; `NullBrain` is its safe terminal fallback.
- `BorinForgeBrainAdapter` adapts the older `IForgeBrain` contract.
- `ForgeBrain` and `ForgeBrainManager` contain an older OpenAI-compatible LM Studio HTTP integration. That implementation mixes provider transport with Forge intent routing and repository context, so it should not be expanded into the new cloud-provider boundary.
- `LocalAiProvider` now adapts any existing `IBrain` to `IAiProvider`.
- `NvidiaNimProvider` now implements the same `IAiProvider` boundary without owning state or action authority.

### Player action selection and command execution

- `BorinPlayerResearch` calls the brain for higher-level player decisions and parses a constrained decision response.
- `BorinForgeActionRouter` classifies Forge connection and directed game actions locally.
- Supervised and autonomous commands pass through `BorinPlayerResearch` checks and then `IClientSessionService.SendCommandAsAsync` with a recorded command origin.
- The Host/BBS remains authoritative for game command validity and consequences.
- Autonomous action families are more conservative than explicitly supervised user commands.

NVIDIA output must later be normalized into the same decision/action path. `NvidiaNimProvider` has no terminal reference and cannot send commands.

### Forge Borin UI

- `ForgeGuardianView` and `ForgeGuardianViewModel` are the existing Borin surface.
- They expose local brain status/performance, conversation, memory, repository study, engineering workspace/orchestration, player assignment, login/session truth, and supervised command controls.
- `BorinPlayerAssignmentWindow` is a secondary controller over the same view model/player controller, not a separate AI application.

The NVIDIA mode/key/status controls belong in this existing view and view model in the UI phase.

### Developer and engineering tools

- `IForgeRepositoryService` provides bounded repository scan/search/log/context/study operations.
- `IForgeDeveloperTools` provides isolated Git worktree creation, patch validation/application, workspace build/test, diff export, approval-gated main apply, and reverse-patch rollback.
- `BorinEngineeringWorkspace`, `BorinEngineeringOrchestrator`, and `BorinEngineeringCorrectionLoop` implement staged, limited repair workflows and evidence capture.
- Guardian Hall supplies persisted capability switches, engineering assignments, reports, and orchestration.
- Existing tools invoke known command families locally; no cloud model needs unrestricted shell access.

These systems should become callable tools behind permission descriptors rather than be duplicated.

### Sysop, Editor, and Test Realm support

- Player-side Sysop behavior uses ordinary authenticated game commands and the server's authoritative command runtime.
- `ClassicAdministrationEditorService` and the Editor/Guardian surfaces provide application/editor integration points.
- Guardian Hall Worldsmith already models Test/Development/Offline targets, approval codes, proposal/simulation/apply stages, and blocks live targets outside its safety rules.
- Server `RuntimeBuilderContentPolicy` explicitly restricts live reload to `TestRealm` and records audit facts.
- Current Worldsmith behavior is primarily proposal/simulation oriented; it is not yet the complete Borin Test Sysop tool surface described in the handoff.

### Logging, builds, and tests

- Application logging flows through `IAppLogger`; client sessions have bounded terminal scrollback and optional terminal logs.
- Borin player/engineering subsystems maintain local journals and reports.
- The root solution is `MajorMud.Reforged.sln`; the established configuration is Release on .NET 8.
- Existing relevant test surfaces include `Borin.CharacterTests`, compiler tests, core tests, server smoke/audit modes, and engineering workspace build/test commands.
- This bundle adds `Borin.AiProviderTests` for provider transport and security behavior.

## Phase 2-3 additions

- `IAiProvider` and normalized `AiResponse` establish a provider boundary independent of NVIDIA.
- `LocalAiProvider` preserves the current `IBrain` implementation as a provider.
- `NvidiaNimProvider` targets `https://integrate.api.nvidia.com/v1/chat/completions` with model `nvidia/nemotron-3.5-lightning-30b-a3b`.
- The provider accepts an injected `HttpClient` and credential store for cancellation and deterministic testing.
- It sends a compact, verified subset of `BrainContext`; it does not send the entire memory database or raw terminal buffer.
- Authentication, 429, timeout, network, service, missing-model, configuration, and malformed-response failures are classified for the future router/scheduler.
- 429 `Retry-After` metadata is preserved. No internal unbounded retry loop exists.
- `WindowsNvidiaNimCredentialStore` stores only in Windows Credential Manager and exposes a configured-state query for future UI binding.
- `AiSecretRedactor` sanitizes credential-shaped request context and remote error detail.

## Minimum remaining architecture changes

1. `BorinContextBuilder` now packages the existing snapshots and relevance retrieval into bounded provider context; registration in the active hybrid router remains pending.
2. Hybrid/Local Only/NVIDIA Only routing and classified local fallback now exist. Persisted settings, active registration, and Forge controls remain pending.
3. The shared sliding-window rate budget now enforces a 20 RPM normal target and 40 RPM hard ceiling with bounded waits, bursts, cancellation, and status. Active singleton registration remains pending.
4. Structured decision schema and strict parsing are now present; controlled tool argument schemas remain pending. Provider prose is not executed.
5. Local validation now covers role, permission, target, request/current-state fingerprint, and rapid duplicate actions. Broader objective action/failure limits remain pending.
6. Add a monotonic state version to the canonical session/snapshot and reject stale model actions.
7. Wire provider/key/status controls into `ForgeGuardianViewModel` and `ForgeGuardianView`; never display a recovered key.
8. The full permission-filtered capability catalog now maps Player, Sysop, and Developer needs to local authorities. Controlled adapters now cover core player commands, repository reads/search/logs, isolated workspace build/test/diff/patch, and Worldsmith Test Realm monitoring/proposal/approved staging. Richer gameplay, specific runtime/editor inspection, focused tests, test-server lifecycle, issues, and repair-loop adapters remain pending.
9. Add the AI audit trail with concise rationale and secret redaction.

## Explicitly not proven in this bundle

- No live NVIDIA request was made because no user credential was requested or exposed.
- NVIDIA is not yet selectable or active in the Forge UI.
- Hybrid routing, fallback execution, shared RPM enforcement, structured action execution, stale-action rejection, and autonomous repair are not yet implemented.
- No production world or source deployment path was changed.
