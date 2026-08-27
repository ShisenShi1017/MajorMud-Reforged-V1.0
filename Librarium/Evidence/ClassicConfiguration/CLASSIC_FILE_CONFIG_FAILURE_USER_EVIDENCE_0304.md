# Classic file/configuration/failure operator evidence — Update 0304

## Scope

This record captures direct operator corrections supplied during the Pass 33 parity session. It is used only where it narrows or corrects prior speculative Reforged assumptions.

## Native data-file workflow

- MajorMUD does not provide an in-game content import/export or migration workflow for Classic world data.
- Builders edit the native MajorMUD files externally, for example with Nightmare Redux.
- The edited native files are copied into the MajorMUD installation and the server is then started/restarted.
- The file must be the exact file expected by that MajorMUD build and the version must match exactly.
- Wrong/corrupt native files or wrong versions do not enter a graceful conversion path. They commonly cause MajorMUD/Worldgroup startup to fail or Worldgroup to crash, requiring a server restart.
- Exact crash text/log output is not claimed by this evidence.

## Gameplay failure boundary

- Ordinary failed gameplay actions generally leave the game state unchanged; they do not invoke a synthetic rollback/recovery subsystem.
- Exact command-specific failure text versus silent no-op behavior remains evidence-gated.

## Recovery boundary

- Catastrophic recovery is operator-described as rollback to the last server-shutdown save state.
- This is retained as recovery evidence, but Pass 33 does not invent a new Reforged crash-recovery implementation or intentionally crash the Reforged host.

## Configuration ownership

- Classic MajorMUD gameplay/module configuration is controlled by `WCCMMUD.INI`.
- Reforged itself does **not** have or need a `WCCMMUD.INI` file.
- Native `WCCMMUD.INI` / `WCCMMUD.mcv` values are therefore evidence for Classic defaults and semantics, while Reforged projects proven values into its own internal typed configuration/runtime owners.
- `SYSOP RELOAD` must not be wired to a nonexistent Reforged INI file.

## Confidence policy

These operator corrections are authoritative project evidence for the workflow/boundary statements above. They do not prove undocumented file headers, exact native crash messages, every failure message, or unknown configuration ranges/live mutability.
