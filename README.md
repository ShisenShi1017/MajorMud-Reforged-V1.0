# RealmGate

RealmGate is a standalone MajorMUD-compatible game platform built around an authoritative RealmGate server, a cross-platform desktop client in active migration, Creator Studio/editor tooling, the Librarium data/evidence system, and Borin.

## Current baseline

- Code Update: `0142`
- Database Version: `0010`
- Milestone: **CodeUpdate0142 - RealmGate Client and Character Integration**
- Classic parity ledger: **200 PASS / 0 STRONG / 0 UNKNOWN**
- Core/current-health validator: **29/29 PASS**
- RealmGate client regression smokes: **14/14 PASS**
- Cross-platform client-core regression: **62/62 PASS**
- Latest local Avalonia client slice: **81/81 client-core checks**; source-backed progression and bounded external-BBS observations remain read-only. RealmGate combat supports preview-first action rules, cooldowns, defensive actions, and equipment-set rotation; party planning includes cure/rescue, regroup, invitations, and rank. Learned-spell actions use the Host-reported list, buff upkeep uses reported expiry, and redacted support snapshots omit command and conversation text. Cross-platform application defaults preserve unknown WPF fields and use Windows/XDG user configuration roots without restoring the retired theme selector. Secure credentials use Windows Credential Manager or Linux Secret Service and never fall back to files. Avalonia Release builds retain the two known analyzer-version warnings; WPF Release passed with 0 errors and restricted-network package-audit warnings in the latest slice. The complete Classic room/dialogue recovery passed the **29/29** local validator with a clean solution build. Windows/Linux preview publishes preceded these slices. Native Linux GUI and keyring use, a real external-board field test, and pushed CI remain unverified.
- RealmGate app Release build and full solution build: **0 warnings / 0 errors** in the verified CodeUpdate0142 closure
- GitHub Classic parity closure: **PASS** on the pushed CodeUpdate0142 baseline.
- Post-closure local runtime hardening: **PASS** — per-world/session fault containment, Host Sessions/Logs tabs, world-scoped character deletion, and generated LIGHT equipment behavior.
- Classic room/dialogue recovery: **PASS** — 7,679 focused assertions cover 675 text-gate aliases, 831 object aliases, all 1,739 decoded room-action aliases, 1,225 NPC keyword routes, and 37 direct GREET ownership routes across 19 no-keyword roots. PRICE debit, native CAST execution, Ferryman two-way travel, client visibility, and save/reconnect behavior are verified.

The detailed current state is maintained in `PROJECT_STATUS.md`.

## Repository authority

`Librarium/` is the committed gameplay/data authority. Generated runtime worlds under `Data/` are build output and must not be hand-edited or committed.

Imported databases, original WCC/WGS/WBB/native files, external BBS databases, and similar material are reference/import inputs unless they are explicitly promoted through a reviewed RealmGate workflow.

Important boundaries:

- RealmGate server gameplay state is server-authoritative.
- Classic remains immutable as a source world.
- Custom worlds are compiled from Classic plus reviewed overrides into complete standalone runtime datasets; live derived worlds never fall back to `Data/Classic`.
- External BBS MDB data is treated as read-only import/reference input.
- Runtime/local saves are not source code.
- Character deletion is account- and world-scoped; active characters cannot be deleted.
- `Reports/RuntimeVerification/majormud_parity_scorecard.json` remains required by Borin's parity preflight and must not be removed unless Borin is changed to consume another authority.

## Main components

- `Apps/Source/RealmGate.Server/` — authoritative RealmGate game server/runtime (transitional internal path).
- `Apps/Source/RealmGate.Desktop/` — Avalonia Windows/Linux client and Host migration target.
- `Apps/Source/RealmGate.Creator.Core/` — cross-platform Custom-world editor authority shared by WPF and Avalonia.
- `Apps/Source/RealmGate.Mdb.Core/` — pure-managed Access reader shared by protected Creator import and external-BBS overlay workflows.
- `Apps/Source/RealmGate.App/` — RealmGate Windows/WPF client, Host, Editor, Librarium, Borin, settings, diagnostics, and client automation (transitional internal path).
- `Core/` — shared contracts, models, validation, and runtime foundations.
- `Librarium/` — canonical decoded data, assertions, provenance, and runtime definitions.
- `Tools/RealmGateValidator/` — single current-health validator entrypoint.
- `Tools/NightmareBridge/Write-NightmareMdb.ps1` — Custom-world MDB export bridge.
- `AI/` — Borin runtime/training/workspace sources.
- `Tests/` — source-level regression/test harnesses.
- `ThirdPartyLicenses/` — notices for incorporated third-party components.

## RealmGate client capability

The RealmGate client provides its MajorMUD-oriented capability set through RealmGate-owned presentation, shared cross-platform session/transport models, Librarium-backed data projections, and server-authoritative workflows. The Avalonia client is replacing WPF in verified phases; WPF remains the complete transitional application until parity is reached.

Current client areas include:

- Telnet/ANSI/CP437 gameplay and session profiles.
- Character Workshop, equipment management, item finding, planning, timers, and loadout sandboxing.
- Aliases, macros, text triggers, wildcard/regex captures, events, batches, cooldowns, and Emergency Stop.
- Map/Librarium browsing and hazard-aware navigation.
- Selected-world navigation graph delivery from remote RealmGate hosts plus world-qualified Favorite and Stash destinations.
- Avalonia inventory/equipment snapshots, per-world/per-character rules and equipment sets, preview-first plans, and fail-closed shop/bank/stash checks.
- Avalonia Assist views for recovery/buffs, hostile-NPC combat, party support, communication, and live diagnostics using a fresh selected-world character snapshot; timed buff refresh, learned-spell actions, session statistics, and privacy-safe support exports retain Host authority and fail-closed execution.
- Avalonia Character views for identity, vitals, attributes/skills, spells/effects, experience, equipment sets, bounded selected-world item finding/loadout comparison, and read-only external-BBS effective item planning. RealmGate sessions use matching selected-world/character/room snapshots; external profiles use manual override, imported MDB delta, then Classic base and show only state recovered from explicitly requested native screens or native room/combat boundaries.
- Avalonia Creator Studio Custom1 browsing, read-only global/where-used/key-door/monster/control-room indexes, schema-driven override editing, effective-world validation, protected MDB/WCC import, portable change packages, map-qualified room connections, shop inventory, NPC attack/ability collection editing, executable quest-stage browsing, guarded runtime-bound script editing, shared-runtime NPC/weapon simulation, visual map management, capability audit, bulk overrides, stopped-world currency/shop/room-stack maintenance, preview-first textblock replacement, Custom1 world settings, realm-scoped character administration, and Classic local policy/SYSOP grants through the cross-platform editor authority. Search results route back through the same Custom1 editor; Classic gameplay data remains immutable. Custom1 and Classic policy saves require exact `CUSTOM1` or `CLASSIC` confirmation, character deletion requires the exact character name, and all administration mutations require both embedded Hosts stopped. NPC and script saves preserve decoded metadata and write only Custom1 overrides; script operations outside the current server runtime fail closed. The room/exit canvas supports zoom, pan, direct room dragging, and non-destructive auto-arrange while requiring explicit coordinate save. Error-gated generation writes an isolated Test Realm, not live Custom1. Creator Studio can launch the staged projection as `Custom1-Test`, with separate characters/world state under `Saves/Custom/custom1-test`; its lifecycle smoke passed. Publishing requires a validated diff preview, explicit acknowledgement, unchanged fingerprint, and stopped embedded Host, then retains an audit entry and rollback snapshot. Package imports do not publish directly. Classic is never an import target. Creator Core currently passes 30/30 checks. Manual Test Realm gameplay review, a real reviewed live publication, and real-file import/runtime certification remain migration gates.
- Avalonia Librarium browsing is lazy and read-only: category tabs show weapons, armor, items, monsters, rooms, classes, races, spells, abilities, quests, and exits with name-only lists and curated gameplay details. Raw JSON, source paths, evidence, confidence, and provenance metadata are intentionally hidden.
- Avalonia application defaults live in the existing Server surface: default world/profile and Host bind/port/session limits persist through one cross-platform store. Saves preserve WPF-only and future unknown settings; no theme selector or top-level Settings tab was restored.
- Secure values stay out of profiles and settings. The shared credential store selects Windows Credential Manager or Linux Secret Service, preserves established Borin/NVIDIA target migration, and fails closed when a secure backend is unavailable.
- Inventory, equipment-set, shop, stash, bank, sell/buy/discard, and currency automation.
- Recovery, healing, cure, rest/meditate, buff upkeep, and Buff Watchdog.
- Combat decision automation with target, resource, safety, cooldown, retreat, and equipment rules.
- Party coordination, assist/follow, support, regroup, reconnect/resync, and permission-gated remote actions.
- Communications Desk, Creature Intel, diagnostics, searchable scrollback, logs, session statistics, wire inspection, support bundles, customization, and help.
- External-BBS game-data overlays and MDB comparison/import support.

The Host workspace contains Host Control, live Sessions, and application/runtime Logs. A failed custom world or individual Telnet session is reported there without taking healthy worlds or the desktop application down.

## Creator Studio and custom worlds

Creator Studio keeps Classic immutable.

For a full custom-world import, RealmGate requires:

1. an MDB; and
2. the associated WCC/WGS native source files used to establish source provenance.

MDB-only input is not considered a complete custom-world source package. RealmGate fingerprints the selected source files, records provenance, validates gaps/conflicts, and builds the custom world from the approved source set.

The current real-file validation boundary is documented in `PROJECT_STATUS.md`.

Generated world manifests are enforced at runtime: every configured world must declare a standalone, complete dataset with live Classic fallback disabled. A derived world also records its generation-time base world.

## External BBS game data

For non-RealmGate BBS connections, the client may use a local effective game-data overlay:

1. manual local override;
2. imported MDB delta;
3. Classic client base.

The external MDB remains read-only. Reimport refreshes the imported layer while preserving manual overrides.

For a RealmGate server, the client instead consumes the exact generated RealmGate world and fails closed if that world is unavailable.

## Build

From the repository root:

```powershell
dotnet restore .\RealmGate.sln
dotnet build .\RealmGate.sln -c Release
```

The current validator entrypoint is:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\RealmGateValidator\Validate.ps1 -Configuration Release
```

The validator is intentionally exhaustive and can take a long time when decode exhaustion is required.

## Windows release

### Windows and Linux tester preview

The cross-platform Avalonia preview is distributed through the **RealmGate Desktop Tester
Release** GitHub workflow. It builds Borin-free, self-contained `win-x64` and `linux-x64`
archives on matching native runners, verifies that their compact Librarium regenerates every
enabled world, and writes SHA-256 checksums. Testers do not need to install .NET.

From GitHub: open **Actions**, select **RealmGate Desktop Tester Release**, choose **Run
workflow**, enter a version such as `0.2.0-beta.2`, and choose whether to create a prerelease.
Without prerelease creation, download the two packages from the completed workflow's Artifacts
section. With it enabled, download them from the repository's Releases page.

The Linux tester extracts the `linux-x64.tar.gz`, opens the contained `RealmGate` folder, and runs:

```sh
chmod +x RealmGate.Desktop Start-RealmGate.sh
./Start-RealmGate.sh
```

The archive includes `START-HERE.txt`. The first Host start generates local runtime worlds from
the packaged Librarium and can take longer than later starts. The release gate proves this exact
empty-`Data` startup path before creating either archive. The `v0.2.0-beta.1` archives predate that
gate and should not be used. Native Linux GUI and Secret Service
behavior still require real tester verification before the Avalonia build can replace WPF.

Create a local package with:

```powershell
./Tools/Release/Build-RealmGateDesktopPackage.ps1 -Version 0.2.0-beta.2 -RuntimeIdentifier win-x64
```

Build the Linux archive on Linux or use the GitHub workflow so Unix executable permissions are
preserved.

### Transitional WPF release

The normal RealmGate desktop release is a framework-dependent, unpacked `win-x64` publish at:

`Release\RealmGate\RealmGate.exe`

Publish the complete app, including Borin, with:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\Release\Publish-RealmGate.ps1
```

Publish the public beta without Borin or private AI dependencies with:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\Release\Publish-RealmGate.ps1 -PublicRelease
```

The Borin-free output is written separately to `Release\RealmGate-Public\`. Distribute the complete folder because this framework-dependent build requires its accompanying DLLs.

The publisher preserves the antivirus-friendly unpacked layout and reports clearly when the result is unsigned. For a distributable beta, install a trusted code-signing certificate in the Windows certificate store and run:

```powershell
$env:REALMGATE_SIGNING_THUMBPRINT = "YOUR_CERTIFICATE_THUMBPRINT"
powershell -ExecutionPolicy Bypass -File .\Tools\Release\Publish-RealmGate.ps1 -RequireTrustedSignature
```

The signing path uses SHA-256 with an RFC 3161 timestamp, signs the RealmGate executable and first-party RealmGate assemblies, and verifies every signature. No certificate or private-key password belongs in the repository. An unsigned or self-signed executable can still trigger Microsoft Defender SmartScreen; the trusted publisher certificate is the remaining distribution requirement.

## Documentation

The authoritative root documentation is now intentionally small:

- `README.md` — architecture, usage, and repository policy.
- `PROJECT_STATUS.md` — current verified state and remaining work.
- `CHANGELOG.md` — compact CodeUpdate history.
- `VERSION.md` — current version identity.
- `AGENTS.md` — engineering/assistant workflow and verification rules.

Detailed historical pass-by-pass documentation is not part of the current operating baseline. Git history is the historical record.
