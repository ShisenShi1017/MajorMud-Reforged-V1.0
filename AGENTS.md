# RealmGate Agent Instructions

This file is the current engineering/assistant handoff authority for RealmGate. Historical handoff, AI-context, manifest prose, and duplicated release-note files are no longer part of the required startup context.

## Required startup order

1. `AGENTS.md`
2. `PROJECT_STATUS.md`
3. `README.md`
4. `VERSION.md`
5. `ProjectManifest.json`
6. `ValidationManifest.json`
7. Read subsystem/source files relevant to the requested task.

Use `CHANGELOG.md` only when update history materially matters.

## Source of truth

- GitHub/repository source is the code source of truth.
- `Librarium/` is the committed gameplay/data authority.
- `Data/` is generated runtime output and must not be hand-edited.
- RealmGate server state is authoritative during gameplay.
- Imported MDB/WCC/WGS/WBB/native/reference material is evidence or import input unless explicitly promoted through a reviewed workflow.
- Classic is immutable.
- Custom worlds are derived/overlay worlds with provenance.
- Do not infer runtime truth from historical reports when current code, Librarium records, generated data, or validator results disagree.

## Engineering rules

1. Never guess when a result can be verified.
2. State PASS only from an observed successful gate.
3. Do not claim GitHub Actions passed unless the pushed workflow result was actually observed.
4. Prefer small, reviewable, reversible changes.
5. Keep `Core` independent from application/UI projects.
6. Shared contracts/models belong in `Core`; UI-specific behavior stays in the app.
7. Preserve server authority; do not move gameplay authority into client automation.
8. Keep rules/data data-driven when they are content/world behavior rather than engine internals.
9. Preserve third-party license notices for copied or substantially derived code.
10. RealmGate client functionality must be implemented from RealmGate requirements, protocol behavior, session state, and Librarium authority; do not copy third-party client implementations.
11. Never delete a file merely because it is in `Reports/` or looks historical; first verify that no active runtime/trainer/tool consumes it.
12. In particular, Borin currently requires `Reports/RuntimeVerification/majormud_parity_scorecard.json` for parity preflight.

## Verification rules

The current full validator entrypoint is:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\RealmGateValidator\Validate.ps1 -Configuration Release
```

The accepted CodeUpdate0142 closure baseline is:

- RealmGate app Release build: 0 warnings / 0 errors.
- Full solution Release build: 0 warnings / 0 errors.
- Current-health validator: 29/29 PASS.
- RealmGate client smoke suite: 14/14 PASS.
- Classic parity: 200 PASS / 0 STRONG / 0 UNKNOWN.
- Decode exhaustion: 49/49 families and 2332/2332 scalar fields connected or structural.
- Cross-platform client-core regression: 62/62 PASS.

The subsequent local Avalonia Phase 2 assistant batch passed 70/70 client-core checks and the then-current 28/28 full validator on 2026-10-24. Both framework-dependent preview publishes completed. The local Phase 3 Character and assistant expansion now passes 79/79 client-core checks. Its source-backed progression exposes level-band XP, CP, lives, class progress, and generated combat statistics without duplicating formulas. External-BBS item planning reads the effective manual override -> imported MDB delta -> Classic projection and can capture explicitly requested equipment, stat, experience, spell, and party screens plus native room/combat boundaries through bounded shared parsers. Those observations are display-only and are not RealmGate-world or automation authority. Advanced RealmGate combat planning supports cooldown-governed action rules plus default, multi-target, and defensive equipment-set rotation through the existing inventory planner. Party planning supports same-room cure/rescue, explicit regroup, configured visible-player invitations, and preferred rank. Learned-spell actions are limited to the Host-reported spell list, buff upkeep can refresh from reported expiry, and session diagnostics export a privacy-safe snapshot without command or conversation text. The Custom-world editor authority, managed MDB reader, protected MDB/WCC bridge, form catalog, and administration service now build in cross-platform assemblies. Their 30/30 regression covers read-only category/record browsing, global search, where-used references, keyed doors, monster/spawn and control-room indexes, capability audit, NPC collections, executable quest stages, runtime script operations, native evidence inventory, Classic policy/realm-character discovery, and guarded Test Realm preview/publish/rollback without mutating source authority. Avalonia exposes the Custom1 browser, protected import, editing, validation/generation, packages, specialized room/shop/NPC/quest/script tools, read-only World Tools, guarded maintenance, world settings, character administration, Classic policy, combat calculators, visual map management, and guarded publication. The latest Avalonia Release build passed with 0 errors, the two known SDK/analyzer-version warnings, and restricted-network `NU1900` vulnerability-index warnings; the transitional WPF Release build passed with 0 errors and only restricted-network `NU1900` warnings in this slice. The complete Classic room/dialogue recovery passed 7,679/7,679 focused checks and the current 29/29 full validator on 2026-10-25. Native Linux GUI operation, a real external-board field test, and pushed CI for these local changes remain unverified.

The local Creator Phase 4 slice includes native Avalonia MDB/file and WCC/WGS-folder selection, read-only analysis, source-pair verification, and an explicitly acknowledged Custom1-only import action. The existing bridge revalidates the pair immediately before writing; Classic is never selectable and source files remain read-only. Shared form definitions drive Custom1 create/edit fields, saves write changed fields only to Custom1 overrides, effective-world validation lists errors and warnings, and standalone generation is blocked while errors exist. Portable Custom1 packages support scoped export and preview/acknowledge/apply import with the existing pre-import backup. Map-qualified room connections and shop inventory use their shared specialized models. Read-only World Tools provide global effective-world search, where-used references, keyed doors, monster/spawn indexing, control-room indexing, and capability audit with direct Custom1 record navigation. Guarded maintenance provides bulk Custom1 overrides, stopped-world currency/shop/room-stack operations, and preview-first textblock replacement. Custom1 world settings and realm-scoped character administration now share the cross-platform service, while Classic administration changes only local policy and explicit character SYSOP grants. Custom1 and Classic policy saves require exact `CUSTOM1` or `CLASSIC` confirmation, character deletion requires the exact character name, and all administration mutations require both embedded Hosts stopped. Source changes invalidate prior validation. The dedicated NPC editor preserves unknown decoded attack fields, permits repeated native ability IDs, and synchronizes Custom1 compatibility mirrors without changing Classic. Executable quest stages and linked scripts have a dedicated editor; script saves accept only current runtime-bound opcodes, rebuild executable clauses, and fail closed on unsupported behavior. NPC and weapon calculators call the shared recovered runtime resolver. The visual map renders effective room/exit relationships and supports zoom, pan, direct room dragging, numeric coordinate editing, non-destructive auto-arrange, and explicit save without changing gameplay data. Generation now targets `Data/TestRealms/Custom1`; live publication requires validated preview, acknowledgement, unchanged fingerprint, and stopped embedded Host, then stages an exact swap and retains audit/rollback data. Creator Studio can activate the staged projection as `Custom1-Test` with isolated saves/world state and no world-selection ambiguity. Its headless lifecycle smoke verified both listeners, data-authority routing, save isolation, and clean shutdown. Creator Core passes 30/30. The current Test Realm generated 44 families, 112,691 objects, and 60 files, all validated read-only; no live publication was performed. Verification made no policy, character, or Custom1 data mutations. Manual Test Realm gameplay, a real reviewed live publish, real matching-source import/runtime certification, native Linux GUI operation, the full validator, and pushed CI remain unverified.

The Avalonia Librarium now uses a lazy read-only cross-platform browser over generated Classic data. It presents friendly gameplay categories, name-only lists, and curated fields while filtering raw evidence, provenance, confidence, and source metadata. Do not turn this surface into a Data editor; Classic source changes still belong in reviewed Librarium workflows.

The local Phase 5 settings and secure-store slice raises the client-core regression to 81/81. Avalonia persists the default world/profile and Host bind/port/session limits through a shared Windows/XDG store, preserves unknown WPF settings, and keeps the retired theme selector and top-level Settings tab absent. Secure values route through one shared abstraction to Windows Credential Manager or Linux Secret Service via `secret-tool`; no file fallback exists, and established Borin/NVIDIA target names and legacy migration remain intact. WPF and Avalonia Release builds passed with 0 errors; the known analyzer-version and restricted-network package-audit warnings remain. Real Linux keyring interaction and native Linux GUI testing are still open.

The Avalonia tester-release path uses `Tools/Release/Build-RealmGateDesktopPackage.ps1` and the `RealmGate Desktop Tester Release` workflow. It must remain self-contained, Borin-free, and Librarium-first; generated `Data/` is verified in staging and removed before packaging so the embedded Host reproduces it locally. The package gate must launch the packaged application from empty `Data/`, wait for generation and paired-listener startup, and verify clean shutdown before archiving. A successful archive build does not establish native Linux GUI, terminal, gameplay, or Secret Service field certification.

The decode-exhaustion phase is expensive. Do not rerun it simply to reconfirm documentation-only work. Rerun the full validator when runtime/data/validator authority changes or when a change could invalidate the accepted baseline.

For client-only changes, build the transitional `RealmGate.App` project and run the affected client smoke(s). Run the complete client smoke suite before declaring a client release candidate clean.

## Current client smoke flags

- `--smoke-shell`
- `--smoke-settings-layout`
- `--smoke-cleanup-schedule`
- `--smoke-embedded-host`
- `--smoke-client-automation`
- `--smoke-client-map-librarium`
- `--smoke-client-batch3`
- `--smoke-client-inventory-automation`
- `--smoke-client-recovery-automation`
- `--smoke-client-combat-automation`
- `--smoke-client-party-automation`
- `--smoke-client-diagnostics`
- `--smoke-client-parity-completion`
- `--smoke-client-help`

## Creator Studio rules

- Never import into Classic.
- A full custom-world source import requires an MDB plus the associated WCC/WGS native files.
- Preserve SHA-256 source provenance.
- Preview/validate conflicts and gaps before applying.
- Keep `Tools/NightmareBridge/Write-NightmareMdb.ps1`; it is used for Custom-world MDB export.
- The obsolete PowerShell MDB reader is retired; current import uses the managed MDB path.
- Do not claim real-file Custom-world certification until an actual matching MDB + WCC/WGS set has completed import and runtime testing.

## External BBS rules

- Treat the external MDB as read-only input.
- Preserve manual overlay edits across MDB reimport.
- Effective external data precedence is manual override, imported MDB delta, then Classic client base.
- RealmGate-server sessions use the exact generated RealmGate world instead of an external overlay.
- Do not claim real external-BBS MDB certification until a real board MDB has been exercised end-to-end.

## Borin rules

- Borin is part of RealmGate, not a replacement for server authority.
- Do not train around parity/runtime regressions; record/escalate them.
- Preserve persistent memory/state rules unless the user explicitly changes them.
- Keep parity preflight synchronized with the accepted 200/0/0 baseline until the parity authority itself changes.

## Documentation rules

The active root documentation set is:

- `README.md`
- `PROJECT_STATUS.md`
- `CHANGELOG.md`
- `VERSION.md`
- `AGENTS.md`

Update these when the current baseline, architecture, validation status, or next gate materially changes. Do not recreate pass-by-pass historical documentation unless the user explicitly asks for it.

## Packaging and repository updates

- Generated `Data/`, build outputs, local saves, model files, and local smoke-report text are not patch/package source.
- Updated-files-only packages should contain only files changed from the agreed repository baseline.
- Do not commit/push unless the user explicitly requests it.
- When the user chooses a CodeUpdate number, use that number consistently in the current root documentation and manifests.
