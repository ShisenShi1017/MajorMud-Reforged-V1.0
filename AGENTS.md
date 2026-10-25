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
- Current-health validator: 28/28 PASS.
- RealmGate client smoke suite: 14/14 PASS.
- Classic parity: 200 PASS / 0 STRONG / 0 UNKNOWN.
- Decode exhaustion: 49/49 families and 2332/2332 scalar fields connected or structural.
- Cross-platform client-core regression: 62/62 PASS.

The subsequent local Avalonia Phase 2 assistant batch passed 70/70 client-core checks and the 28/28 full validator on 2026-10-24. Both framework-dependent preview publishes completed. A later local Phase 3 Character/item-planning slice passed 72/72 client-core checks and core/server smokes; its Avalonia Release build succeeded with two known analyzer-version warnings. The full validator has not been rerun for that slice. Native Linux GUI operation and pushed CI for these local changes remain unverified.

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
