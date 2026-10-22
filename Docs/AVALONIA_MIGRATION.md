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
- Add remote generated-world graph delivery and port WPF favorite/stash destination management; local generated worlds already load their own graph without Classic runtime fallback.
- Port inventory automation, recovery, combat automation, party, communications, and diagnostics incrementally.

Current focused verification: Client Core terminal/transport/profile/protocol/input-automation/event/navigation regression `56/56`; generated Classic and Custom1 projections both pass read-only compiler validation with independent v2 navigation graphs; the bundled Classic graph contains 26,668 rooms and 61,974 resolved edges. WPF compatibility builds at `0 warnings / 0 errors`, and its complete client smoke suite passes `14/14`. Avalonia builds with only the known SDK 8 versus Avalonia 12 analyzer-version warnings. Windows `win-x64` and Linux `linux-x64` framework-dependent publishes both complete successfully and include the v2 Classic navigation graph.

## Phase 3 — Character

- Port Summary, Equipment Planner, Statistics, Spells & Effects, and Progress.
- Reuse RealmGate/Librarium projections; do not duplicate character rules in UI code.

## Phase 4 — Creator Studio + importer

- Extract MDB/ACCDB reader/importer and Custom1 editor services from `RealmGate.App` into cross-platform service projects.
- Replace `Microsoft.Win32` dialogs with Avalonia storage-provider APIs.
- Port editor views against the extracted services.

## Phase 5 — Librarium, settings, private development surfaces

- Port Librarium browser/tools and settings.
- Abstract secure credential storage: Windows Credential Manager on Windows and Secret Service/keyring on Linux.
- Port Borin/private AI only after public functionality is stable, preserving build-time exclusion for public releases.

## Phase 6 — release replacement

- Add Windows installer/portable packaging from `RealmGate.Desktop`.
- Add Linux tarball/AppImage packaging.
- Add optional Linux headless server package.
- Run full Windows/Linux smoke, networking, terminal, importer/editor, and gameplay parity gates.
- Retire WPF only when the Avalonia Definition of Done reaches functional parity.
