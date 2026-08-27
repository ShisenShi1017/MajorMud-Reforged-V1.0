# Editor

The active editor is integrated into the Windows `Reforged.App` workspace rather than shipped as a separate editor executable.

## CodeUpdate0279

The Editor workspace is now functional and starts with two scopes:

- **Custom World 1** — edit one isolated Classic-derived custom realm, browse canonical Librarium objects, author world-scoped overrides, and build `Data/Custom1` for Host testing.
- **Classic Administration** — retain the existing Classic item-limit, SYS GOTO, and character SYSOP controls.

Canonical Custom1 differences live in `Librarium/WorldOverrides/Custom1/overrides.json`. Generated `Data/Custom1` is disposable runtime output and should not be hand-edited. Classic compilation does not consume Custom1 overrides.

The historical standalone editor scaffold under `Apps/Source/Reforged.Editor/` remains legacy scaffolding; new editor work should target the integrated `Reforged.App` Editor workspace unless the architecture is deliberately split again later.
