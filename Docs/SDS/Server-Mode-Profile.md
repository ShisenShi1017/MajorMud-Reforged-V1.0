# Server Mode Profile

Status: Draft architecture direction folded into CodeUpdate-0039.

## Direction

The first real Server target is the Classic baseline. The Server should load and run Classic data first.

Do not implement Reforged mode, Custom mode, AI systems, alternate gameplay rules, or advanced behavior until explicitly approved.

## Architecture Rule

Classic is the first runtime baseline, but the implementation should stay mode-ready. Avoid hardcoding Classic in a way that blocks future mode/profile loading.

Only simple extension points needed for future mode/profile loading should be added before the Classic baseline is working.

The Server should still be designed as a reusable engine. Classic is the first verified target, not a reason to create a Classic-only server codebase.

## Responsibility Split

- `Data/` defines world content.
- Server defines and enforces engine behavior.
- Editor should eventually edit data safely.

## Intended Evolution

1. Classic server baseline.
2. Mode/profile loader.
3. Reforged mode as Classic-derived data/rules overrides.
4. Custom/editor-created worlds.
5. AI systems and advanced behavior only after explicit approval.

## Boundary

This is a planning document only. CodeUpdate-0039 does not implement mode loading, Reforged mode, Custom mode, AI systems, alternate gameplay rules, or runtime gameplay systems.
