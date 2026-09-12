# RealmGate

RealmGate is a standalone MajorMUD-compatible game platform built around an authoritative RealmGate server, a modern Windows client, Creator Studio/editor tooling, the Librarium data/evidence system, and Borin.

## Current baseline

- Code Update: `0142`
- Database Version: `0010`
- Milestone: **CodeUpdate0142 - RealmGate Client and Character Integration**
- Classic parity ledger: **200 PASS / 0 STRONG / 0 UNKNOWN**
- Core/current-health validator: **26/26 PASS**
- RealmGate client regression smokes: **14/14 PASS**
- RealmGate app Release build and full solution build: **0 warnings / 0 errors** in the verified CodeUpdate0142 closure
- GitHub Classic parity closure: **PASS** on the pushed CodeUpdate0142 baseline.
- Post-closure local runtime hardening: **PASS** — per-world/session fault containment, Host Sessions/Logs tabs, world-scoped character deletion, and generated LIGHT equipment behavior.

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
- `Apps/Source/RealmGate.App/` — RealmGate Windows/WPF client, Host, Editor, Librarium, Borin, settings, diagnostics, and client automation (transitional internal path).
- `Core/` — shared contracts, models, validation, and runtime foundations.
- `Librarium/` — canonical decoded data, assertions, provenance, and runtime definitions.
- `Tools/RealmGateValidator/` — single current-health validator entrypoint.
- `Tools/NightmareBridge/Write-NightmareMdb.ps1` — Custom-world MDB export bridge.
- `AI/` — Borin runtime/training/workspace sources.
- `Tests/` — source-level regression/test harnesses.
- `ThirdPartyLicenses/` — notices for incorporated third-party components.

## RealmGate client capability

The RealmGate client provides the MajorMUD-oriented capability set developed through the legacy MudPlay parity work while keeping RealmGate's own WPF layout, terminology, architecture, and server-authoritative model.

Current client areas include:

- Telnet/ANSI/CP437 gameplay and session profiles.
- Character Workshop, equipment management, item finding, planning, timers, and loadout sandboxing.
- Aliases, macros, text triggers, wildcard/regex captures, events, batches, cooldowns, and Emergency Stop.
- Map/Librarium browsing and hazard-aware navigation.
- Inventory, equipment-set, shop, stash, bank, sell/buy/discard, and currency automation.
- Recovery, healing, cure, rest/meditate, buff upkeep, and Buff Watchdog.
- Combat decision automation with target, resource, safety, cooldown, retreat, and equipment rules.
- Party coordination, assist/follow, support, regroup, reconnect/resync, and permission-gated remote actions.
- Communications Desk, Creature Intel, diagnostics, searchable scrollback, logs, session statistics, wire inspection, support bundles, customization, and help.
- External-BBS game-data overlays and MDB comparison/import support.

MudPlay is a legacy implementation reference, not RealmGate's UI or product identity. Required third-party notices are retained in `ThirdPartyLicenses/`.

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
