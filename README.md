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
- Latest local Avalonia Character/item-planning slice: **72/72 client-core checks and core/server smokes**; Avalonia Release build succeeded with two known analyzer-version warnings. The subsequent complete Classic room/dialogue recovery passed the **29/29** local validator with a clean solution build. Windows/Linux preview publishes preceded these slices. Native Linux GUI use and pushed CI remain unverified.
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
- Avalonia Assist views for recovery/buffs, hostile-NPC combat, party support, communication, and live diagnostics using a fresh selected-world character snapshot; opt-in continuous execution stops on disconnect, Emergency Stop, identity mismatch, or repeated ineffective attempts.
- Avalonia Character views for identity, vitals, attributes/skills, spells/effects, experience, equipment sets, and bounded selected-world item finding/loadout comparison, using matching selected-world/character/room snapshots.
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
