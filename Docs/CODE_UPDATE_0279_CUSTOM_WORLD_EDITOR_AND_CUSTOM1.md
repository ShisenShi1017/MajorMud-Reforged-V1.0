# CodeUpdate0279 — Custom World Editor Foundation and Custom1

## Purpose

Start the integrated Reforged editor and enable one host-editable custom world without weakening the protected Classic realm.

## Enabled world

`Config/server.worlds.json` now enables `Custom1` as `ReforgedCustom` alongside Classic.

- Base rules profile: `classic`
- Generated runtime root: `Data/Custom1`
- AI mode: Off
- Classic protection: false for Custom1; Classic remains protected
- Character transfer into Classic remains prohibited by Classic's runtime boundary

Custom1 begins from the same Librarium-derived runtime shape as Classic. It differs only where a Custom1 override is authored.

## Canonical custom edits

Custom world differences are stored under:

`Librarium/WorldOverrides/Custom1/overrides.json`

The initial schema supports object-scoped operations:

- `patch` — recursively merge only the supplied fields into an existing Librarium record
- `upsert` / `create` — create a Custom1 record when the target does not exist
- `delete` — remove the target from the Custom1 projection

Classic compilation explicitly ignores world override files. `/Data` remains generated/disposable output and is never the authoring source.

## Compiler integration

`WorldOverrideEngine` now applies Custom1 deltas after canonical Librarium loading and before runtime projection/validation. Specialized item/equipment generation consumes the same effective Custom1 source families, preventing item edits from being lost in the specialized projections.

Custom1 also uses the Classic runtime-family exclusion shape so merely enabling the world does not introduce Librarium-only authoring families into runtime data.

Compiler commands now document `Custom1`, and the server GitHub workflow generates both Classic and Custom1 runtime data.

## Integrated editor

The existing Reforged desktop **Editor** workspace is now wired to a real `EditorViewModel` and contains two top-level areas:

### Custom World 1

**World Settings**

- world name
- description
- login banner
- MOTD
- runtime-enabled switch
- reserved Custom-world transfer switch
- Custom1 build/rebuild action
- generated-data state/path display

**Content Editor**

- choose a supported runtime Librarium family
- search/browse base objects by ID or name
- inspect the complete base JSON record
- select an object and author only the Custom1 fields that differ
- save/remove scoped overrides
- build/rebuild `Data/Custom1` for immediate Host testing

The first picker exposes runtime-useful families including rooms, exits, NPCs, spawns, items, spells, abilities, classes, races, shops, quests, triggers, scripts, skills, formulas, and commands.

### Classic Administration

The prior Classic item-limit, SYS GOTO, and per-character SYSOP controls remain available on a separate tab.

## Host startup

The embedded Host now checks runtime-enabled `ReforgedCustom` worlds before runtime bootstrap. If Custom1's generated room/manifest data is missing, the Host compiles it from Librarium automatically. Authored changes should still use **Build / Rebuild Custom1** so an existing generated projection is refreshed before testing.

The network world selector already recognizes `Custom1`, so after generation the world can be selected from the existing Host/Play flow.

## Regression coverage

A compiler regression fixture was added for recursive Custom1 patching. It verifies that a nested field can change while untouched nested Classic fields remain present, and that the override engine reports the applied delta.

## Validation performed in the packaging environment

- JSON parse: PASS for world configuration and new Custom1 override/overlay metadata
- XAML parse: PASS for integrated Editor view
- project XML parse: PASS
- WPF binding-name static check: PASS
- GitHub workflow YAML parse: PASS
- modified C# delimiter/static structure checks: PASS

A full .NET compile/test could not be executed in the packaging environment because no `dotnet`, `csc`, or `mcs` executable is installed. The focused compiler test is included for local/CI execution where the .NET 8 SDK is available.

## Next editor passes

This update is the editor foundation, not the final Nightmare Redux-style authoring surface. Logical next passes are typed family editors (beginning with rooms/exits), creation/clone workflows, validation previews, live test-realm restart/reload controls, and then richer visual editors for NPCs, items, spells, quests, scripts, and world events.
