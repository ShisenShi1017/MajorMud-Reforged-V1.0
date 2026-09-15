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

- Move client connection/profile services behind cross-platform interfaces.
- Port Telnet session handling without WPF dependencies.
- Implement an Avalonia ANSI terminal control using the existing terminal parsing/state model.
- Preserve raw CP437/CRLF behavior and MajorMUD ANSI parity tests.
- Port aliases, macros, triggers, events, navigation, inventory automation, recovery, combat automation, party, communications, and diagnostics incrementally.

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
