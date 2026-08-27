# Code Update 0313 — Classic world-rules/configuration boundary

Pass 42 promotes the broad native MajorMUD world-rules/configuration ownership boundary without introducing a legacy INI file or guessing undocumented setting semantics.

## Promoted boundary — STRONG

`native.world-rules-configuration` advances **UNKNOWN → STRONG**.

Retained `WCCMMUD.mcv` evidence preserves **44 exact setup prompts/defaults** for the native `WCCMMUD.INI` configuration surface. Reforged intentionally does **not** ship, create, read, or write `WCCMMUD.INI`; the native file is evidence only and proven values are projected into Reforged-owned runtime authorities.

Six exact defaults are already connected to live Classic owners:

- Player-data save interval: **60 seconds** → `RuntimeClassicTimingAuthority`.
- Buffer-data save interval: **300 seconds** → `RuntimeClassicTimingAuthority`.
- Maximum PvP level difference: **100** → live Classic PvP default policy.
- Maximum Evil Points forgiven per day: **18** → `RuntimeCharacterAlignmentAuthority`.
- Evil Point cycle: **60 minutes** → `RuntimeCharacterAlignmentAuthority`.
- Evil Points forgiven per cycle: **1** → `RuntimeCharacterAlignmentAuthority`.

The remaining retained setup defaults stay catalog evidence unless their gameplay owner/effect is independently recovered. Generic Reforged world-rule ranges or mutability metadata are not promoted as native behavior.

## Still UNKNOWN

- exact allowed ranges for the remaining WCCMMUD setup values;
- exact live-mutable versus restart-only behavior for the remaining settings;
- exact runtime owner/effect mapping for setup prompts not yet connected to a proven Classic owner.

No `WCCMMUD.INI` file was added. No unproven configuration effect was activated.

## Score

Before Pass 42: **97 PASS / 82 STRONG / 21 UNKNOWN = 200**.

After Pass 42: **97 PASS / 83 STRONG / 20 UNKNOWN = 200**.

- strict parity: **48.5%**
- evidence-backed: **90.0%**
