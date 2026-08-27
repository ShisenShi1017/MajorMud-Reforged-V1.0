# Classic world-rules/configuration evidence — Pass 42 / Update 0313

## Native evidence

`Librarium/Evidence/native_support_catalog_0020.json` SHA-pins `WCCMMUD.mcv` as `8e0daa7cd59fdf1aec947ad46a1e7c61b3eb519d2bb084ee05a5324d2038a30b` and retains 44 printable MajorMUD configuration prompts/defaults. The native configuration target is `WCCMMUD.INI`.

Pass 33 established the project boundary that Reforged must not create/read/write that legacy INI and that wrong/corrupt/wrong-version native files are not migration inputs. Pass 42 does not alter that rule.

## Exact live projections already present

Six retained defaults are consumed by live Classic runtime owners: 60-second Player-data saves, 300-second Buffer-data saves, PvP maximum level difference 100, maximum 18 Evil Points forgiven per day, a 60-minute Evil Point cycle, and 1 Evil Point forgiven per cycle.

These narrow projections are directly traceable to `RuntimeClassicConfigurationAuthority` and their consuming owners. They are enough to establish a strongly evidenced configuration-ownership boundary, but not full native configuration parity.

## Evidence gates

Still unresolved: allowed ranges for remaining settings, per-setting runtime mutability/restart behavior, and owner/effect mappings for prompts that are retained but not connected. Reforged's generic typed world-rule service is an implementation facility and is not used to invent those native answers.

`native.world-rules-configuration`: **UNKNOWN → STRONG**. No `WCCMMUD.INI` file is added.
