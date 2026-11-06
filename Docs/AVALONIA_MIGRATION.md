# RealmGate Avalonia Migration

Branch: `port/avalonia-crossplatform-desktop`

## Goal

Replace the WPF presentation layer with one Avalonia desktop application that runs on Windows and Linux while retaining the existing RealmGate server, compiler, Librarium authority, gameplay behavior, importer/editor capabilities, client automation, and public/private feature boundaries.

## Migration rules

- Do not delete `RealmGate.App` until the Avalonia replacement reaches functional parity.
- Shared server/gameplay/network/terminal behavior must remain outside UI-framework code.
- No socket listener, combat prompt state, or gameplay formulas belong in Avalonia views.
- Librarium remains authoritative; `Data/` remains generated runtime output.
- Windows and Linux builds must be checked in CI on every migration PR.
- Preserve the public-release boundary around Borin/private AI features.

## Phase 1 — cross-platform shell + host

- [x] Add `RealmGate.Desktop` targeting generic `net8.0`.
- [x] Use Avalonia 12.1.1 for Windows/Linux desktop.
- [x] Add Play, Character, Creator Studio, Server, and Librarium shell tabs.
- [x] Port embedded server start/stop behind a UI-independent service.
- [x] Expose Local Only (`127.0.0.1`) and LAN/Remote (`0.0.0.0`) presets.
- [x] Keep bind address, port, max sessions, LAN discovery, and bind errors driven by shared runtime networking.
- [x] Stop the listener when the Avalonia window closes.
- [x] Add Windows `win-x64` and Linux `linux-x64` CI build/publish jobs.

## Phase 2 — Play / terminal

- [x] Move TCP/Telnet connection ownership and the live terminal session into `RealmGate.Client.Core`.
- [x] Move connection-profile models, normalization, migration, corrupt-file recovery, and atomic JSON storage into `RealmGate.Client.Core`.
- [x] Move private client-protocol frame filtering and echoed private-command suppression into `RealmGate.Client.Core` so protocol payloads never render as terminal text.
- [x] Load, select, apply, and save the shared profile catalog from the Avalonia Play tab.
- [x] Port Telnet negotiation, IAC parsing/escaping, terminal type, NAWS, binary, suppress-go-ahead, echo, and RealmGate GMCP announcements without WPF dependencies.
- [x] Implement an Avalonia ANSI terminal control using the shared terminal parsing/state model.
- [x] Connect the Avalonia Play tab to live host/port sessions with command, raw-key, resize, password-masking, and disconnect handling.
- [x] Preserve raw CP437/CRLF behavior and MajorMUD ANSI parity tests.
- [x] Move alias, macro, and text-trigger models, bounded expansion/matching, cooldown execution, and profile-preserving storage into `RealmGate.Client.Core`.
- [x] Add the Avalonia automation editor and execute saved aliases, macros, and completed-line text triggers through the normal terminal command path.
- [x] Move timed-event models, profile persistence, deterministic scheduling, command throttling, and session reset behavior into `RealmGate.Client.Core`.
- [x] Add the Avalonia timed-event editor and guarded runtime clock; events stay paused until gameplay is verified, never transmit while disconnected, do not catch up in bursts, and fail closed when a combat restriction cannot be proven.
- [x] Move map-qualified Classic route-graph loading, room search, key normalization, and deterministic shortest-path planning into `RealmGate.Client.Core`.
- [x] Add the Avalonia Classic Goto surface with private RealmGate room-state requests, exact per-step room confirmation, combat pause, manual-movement cancellation, disconnect cancellation, and topology-mismatch shutdown.
- [x] Generate a standalone `navigation_graph.json` for every compiled world from promoted room, exit, spawn, NPC-level, room-effect, gate, and prerequisite data.
- [x] Add weighted hazard-aware routing with explicit risky fallback, bounded verified route recovery, combat-aware Auto-Roam, and persisted world-qualified recorded routes.
- [x] Keep every automated movement fail-closed behind an exact map-qualified `ROOMSTATE` confirmation; route recording and replay also require exact source and destination rooms.
- [x] Add compressed, chunked, SHA-256-verified remote generated-world graph delivery over the private RealmGate protocol without derived-world Classic fallback.
- [x] Port world-qualified Favorite and Stash destination management with exact ROOMSTATE admission and atomic local persistence.
- [x] Add a structured selected-world/character inventory snapshot channel while retaining the existing WPF-compatible panel frame.
- [x] Port inventory/equipment models, isolated profile storage, equipment-set capture/apply, and fail-closed rule planning into `RealmGate.Client.Core`.
- [x] Add the Avalonia Inventory tab with refresh, preview/run controls, Emergency Stop/pacing integration, and explicit opt-in for sell/stash/discard/buy/deposit actions.
- [x] Add a selected-world/character automation snapshot with resources, combat/rest state, learned spells, effects, room NPCs/players, party, and meditation capability.
- [x] Add shared recovery/cure/heal/buff and guarded hostile-target planners, same-room party support, diagnostics, and isolated local settings.
- [x] Add Avalonia Assist views with preview/run-once, explicit Start/Stop, Emergency Stop, fresh-snapshot checks, command pacing, bounded repeated attempts, and supported Say/Telepath/Gossip/Yell actions.
- [x] Port advanced RealmGate combat action/equipment rotation and same-room party invitation/regroup/rescue policies with fresh authoritative snapshots and preview-first execution.
- [x] Add learned-spell actions, reported-expiry buff refresh, session statistics, and privacy-safe support-snapshot export.
- [ ] Keep external-BBS automation authority gated until a real-board field test proves safe semantics.
- [ ] Verify interactive Linux desktop operation on a native Linux display.

The combined assistant batch passed consolidated local verification. The server snapshot is read through the existing selected-world runtime authorities and respects room visibility. It does not change Librarium or generated gameplay definitions. Assistant commands bypass aliases so execution matches the preview, and a fresh snapshot is required after each action. Profiles are scoped to world and character; continuous execution never starts automatically after login or reconnect.

Phase 2 verification: Client Core terminal/transport/profile/protocol/input-automation/event/navigation/inventory/assistant regression `70/70`; server smoke reconstructs the selected-world navigation graph and validates world, character, and room identity for inventory and automation frames. Generated Classic and Custom1 projections pass with independent v2 navigation graphs; the bundled Classic graph contains 26,668 rooms and 61,974 resolved edges. The full current-health validator passed `28/28`, while the accepted WPF client smoke baseline remains `14/14`. Avalonia builds report the known SDK 8 versus Avalonia 12 analyzer-version warnings. Windows `win-x64` and Linux `linux-x64` framework-dependent publishes completed before Phase 3. Native Linux GUI interaction remains untested.

## Phase 3 — Character

- [x] Add a read-only selected-world character snapshot for race/class, sex, alignment, attributes, skills, level, and experience.
- [x] Port Summary, Statistics, Spells & Effects, Progress, and the existing inventory equipment-set planner into the Avalonia Character tab.
- [x] Add bounded selected-world item search and exact-ID read-only loadout comparison without a local Classic fallback.
- [x] Add source-backed richer progression with level-band XP, next-promotion CP, available CP, lives, class progression, derived statistics, resistances, and saving throws.
- [x] Add read-only external-BBS effective item planning with manual override, imported MDB delta, then Classic base precedence.
- [x] Share the bounded Classic item-screen parser and resolve explicitly requested external-BBS equipment into read-only current-versus-planned comparisons.
- [x] Share Classic stat, experience, spellbook, party, room-display, and combat-boundary parsers and present their external-BBS observations without promoting them to automation authority.
- Reuse RealmGate/Librarium projections; do not duplicate character rules in UI code.

The Character tab combines only matching world/character/room snapshots and clears details on profile or connection changes. The server computes current/next level thresholds and next-promotion CP from the selected world's generated progression authority, and supplies available CP, lives, class progression, derived statistics, resistances, and saving throws from the persisted character. Unavailable thresholds are shown as unavailable, not guessed. RealmGate item searches use the Host's selected-world generated catalog, cap results at 75, and return equipped definitions for read-only comparison. External-BBS profiles instead read the existing local effective projection with manual override -> imported MDB delta -> Classic precedence, including imported-only records. Their current equipment and character state come only from explicit bounded native screens or native room/combat boundaries; these observations never create qualified RealmGate automation authority. Advanced RealmGate combat planning uses ordered action rules, per-action cooldowns, defensive actions, and default/multi-target/defensive equipment rotation through the existing inventory planner. Party planning uses current-room authority for cure/rescue and configured visible-player invitations, and supports explicit regroup and preferred rank. The spells/effects view can issue a user-selected Host-reported spell, buff rules can refresh before a reported expiry, and Diagnostics exports content-free support metadata. Local focused verification: client-core regression passed 79/79, the transitional WPF Release build passed with 0 warnings and 0 errors, recovery/diagnostics smokes exited cleanly, and Avalonia built with 0 errors plus the two known analyzer-version warnings. Native Linux UI operation, a real external-board field test, and pushed CI remain unverified.

## Phase 4 — Creator Studio + importer

- [x] Extract the Custom1 editor authority and form catalog into a cross-platform service project; add read-only Avalonia workspace/category/record browsing.
- [x] Extract the managed MDB/ACCDB reader and protected MDB/WCC bridge from `RealmGate.App`, while keeping Creator import and external-BBS overlays distinct.
- [x] Replace Creator MDB and WCC/WGS selection dialogs with Avalonia storage-provider APIs.
- [x] Port read-only MDB analysis, native evidence inventory, source-pair validation, and the acknowledged Custom1-only import action without exposing Classic as a target.
- [x] Port schema-driven Custom1 editing, effective-world validation, and error-gated standalone world generation against the extracted services.
- [x] Port portable Custom1 package export plus preview/acknowledge/apply import with automatic pre-import backup.
- [x] Port map-qualified room-connection and shop-inventory specialized editors.
- [x] Port NPC combat and weapon simulation through the shared recovered runtime resolver.
- [x] Port effective-world map selection and explicit coordinate save without changing gameplay room data.
- [x] Port NPC attack/ability collection editing with Custom1-only saves and compatibility-mirror synchronization.
- [x] Replace destructive map auto-arrange with a non-destructive preview plus explicit save.
- [x] Port executable quest-stage browsing and guarded runtime-bound script-operation editing without exposing raw JSON or inventing unsupported behavior.
- [x] Add visual room/exit rendering with zoom, pan, direct room dragging, and explicit coordinate save.
- [x] Port read-only global search, where-used references, keyed-door lookup, monster/spawn indexing, and control-room indexing with direct record navigation.
- [x] Port capability audit, bulk Custom1 overrides, stopped-world room currency/shop/stack maintenance, and preview-first textblock replacement behind explicit confirmation.
- [x] Port Custom1 world settings, realm-scoped character administration, and Classic local policy/SYSOP administration through the shared cross-platform service with stopped-Host and typed-confirmation guards.
- [x] Separate Test Realm generation from live Custom1, with validated diff preview, fingerprint recheck, stopped-Host guard, staged swap, audit entry, retained snapshot, and rollback.
- [x] Add a direct interactive Test Realm host/session workflow with isolated runtime identity, generated-data activation, saves, and world state. Field-certify manual gameplay and a real reviewed publication; portable packages still do not deploy directly.

Phase 4 builds `RealmGate.Creator.Core` and `RealmGate.Mdb.Core` on generic `net8.0` and keeps WPF on the same service implementations. Avalonia loads Custom1 only on request, lists editable content families, searches bounded record sets, and presents gameplay fields without exposing evidence metadata. Read-only World Tools cover global effective-world search, where-used references, keyed doors, monster/spawn indexing, control rooms, and capability parity, and route results back through the same Custom1 editor. Global search streams unchanged Librarium records and merges only actual overrides instead of cloning the full world. Guarded maintenance exposes bulk overrides, stopped-world currency/shop/room-stack operations, and preview-first textblock replacement. Custom1 settings, realm-scoped character administration, and Classic local policy/SYSOP grants now use shared cross-platform authority. Settings and policy saves require exact `CUSTOM1` or `CLASSIC` confirmation, character deletion requires the exact display name, and every administration mutation requires both embedded Hosts stopped. Classic gameplay data remains immutable, and source override changes invalidate the previous validation result. Native storage-provider selection drives read-only MDB analysis and WCC/WGS evidence inventory. Pair validation requires complete native family coverage and at least one exact unique source anchor; the import action is enabled only after explicit Custom1-only acknowledgement and the bridge revalidates immediately before writing. The shared schema drives create/edit forms; saves write only changed Custom1 override fields, validation evaluates the effective world, and standalone generation is blocked while validation has errors. Portable change-package import is previewed, invalid entries are blocked, conflicts require explicit acknowledgement, and the shared service retains a pre-import backup. Map-qualified room connections and shop inventory use their existing specialized service models. The dedicated NPC editor loads and saves attack/ability collections without exposing raw JSON, preserves unknown decoded row fields, permits repeated native ability IDs, and synchronizes the compatibility mirrors used by existing runtime readers. NPC and weapon calculators call the shared recovered runtime combat resolver. Map editing renders the effective room/exit graph, supports zoom/pan/direct room dragging, and keeps both manual and automatic layout changes in memory until explicit save; gameplay data is separate. Executable quests expose ordered stages and linked runtime scripts; script saves accept only current runtime-bound opcodes, preserve decoded metadata, rebuild executable clauses, and fail closed on unsupported or newly invented random-table behavior. Generation now writes an isolated Test Realm. Publishing requires validated preview, acknowledgement, unchanged fingerprint, and a stopped embedded Host; it stages an exact swap, audits the action, and retains rollback data. Creator regression passes 30/30, including World Tools, capability audit, administration discovery, and synthetic publish/rollback. The current Librarium generated 44 families, 112,691 objects, and 60 Test Realm files, all of which passed read-only projection validation; live Custom1 was not published. Avalonia can launch that staged projection as the isolated `Custom1-Test` runtime with separate saves and no world-selection ambiguity; the headless lifecycle smoke verified activation, both listeners, data routing, save isolation, and shutdown. Verification made no policy, character, or Custom1 data changes. Manual gameplay review, a real reviewed live publish, real matching MDB + WCC/WGS import/runtime testing, and native Linux interaction remain required.

## Phase 5 — Librarium, settings, private development surfaces

- [x] Port the lazy read-only Classic Librarium browser with friendly category tabs, name-only record lists, curated gameplay details, chunk-manifest support, and no raw evidence metadata.
- [x] Port cross-platform application defaults without restoring the retired theme selector or top-level Settings tab. Preserve unknown WPF settings during saves and use native Windows/XDG user configuration roots.
- [x] Abstract secure credential storage: Windows Credential Manager on Windows and Secret Service/keyring through `secret-tool` on Linux, with no file fallback.
- Port Borin/private AI only after public functionality is stable, preserving build-time exclusion for public releases.

Phase 5 settings use one shared client-core store for the default world, default connection profile, Host bind address, port, and maximum sessions. The Avalonia Server surface loads and saves those values, and embedded Host startup uses the selected default world. The store updates only fields it owns, preserving WPF-only cleanup policy and future unknown settings in the same `application.json`. Existing Borin password and NVIDIA key adapters now share the platform credential abstraction while retaining their established target names and legacy migration. No credential falls back to a file or enters tests. Focused verification passes 81/81 client-core checks; WPF and Avalonia Release builds pass with 0 errors. The known analyzer-version and restricted-network package-audit warnings remain. Real Linux Secret Service interaction and native Linux GUI operation remain unverified.

## Phase 6 — release replacement

- [x] Add self-contained Windows portable packaging from `RealmGate.Desktop`.
- [x] Add a self-contained Linux `tar.gz` package with a launcher and tester instructions.
- [x] Add a native-runner GitHub workflow that can retain packages as artifacts or attach both to a prerelease.
- Add the final Windows installer and optional Linux AppImage after native tester feedback.
- Add optional Linux headless server package.
- Run full Windows/Linux smoke, networking, terminal, importer/editor, and gameplay parity gates.
- Retire WPF only when the Avalonia Definition of Done reaches functional parity.

The tester-package builder stages a Borin-free Avalonia application, compact authoritative
Librarium, writable clean Custom1 override root, configuration, license notices, and the empty
runtime directory contract expected by server bootstrap. It compiles every enabled world inside
that exact staged root, verifies required world and character-foundation output, proves Librarium
immutability, then removes generated `Data/` so first Host start reproduces it locally. Both
packages are self-contained and include SHA-256 checksums. Native Linux GUI/keyring operation and
the complete manual Linux gameplay pass remain field-verification gates; packaging alone does not
claim those results.
