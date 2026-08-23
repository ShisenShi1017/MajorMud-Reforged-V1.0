# CodeUpdate0256 — Classic Character LOOK Repair

## Scope

This incremental update repairs `look <character>` and `examine <character>` in the Classic runtime.

## Runtime behavior

- A unique leading abbreviation of a visible character name resolves to that character. `look s`, `look sh`, and `look shis` can all resolve `Shisenshi` when the abbreviation is unique.
- Direction abbreviations remain valid. A one-letter direction is treated as a character abbreviation only when another visible character matches it; otherwise it keeps its directional meaning.
- Character matches take precedence over similarly named room items or NPCs.
- Ambiguous character prefixes fail clearly and request a longer abbreviation.
- Missing or mismatched race-description data no longer aborts character inspection or hide equipment.
- Race lookup accepts canonical names, separator variants, legacy numeric IDs, `race.<number>`, and generated race IDs.
- Classic stat abbreviations are consumed directly: `str`, `agl`, `int`, `wil`, `hea`, and `cha`.
- Character appearance begins with a stat-derived build, race, and class headline, followed by the remaining stat and wound descriptions.
- Equipped items are rendered after appearance and optional MajorMUD Plus descriptions.

## Data authority

`Data/Classic/race_descriptions.json` contains 13 race narratives extracted from verified native WCC message payloads in `Librarium/Evidence/native_message_catalog_correlation_0022.json`.

## Validation

The installer runs:

1. Release build of `Reforged.Server.Runtime`.
2. Release build of `Reforged.Server`.
3. Release build of `Reforged.App`.
4. Bundle F Phase F2A static-description audit.
5. Bundle F Phase F2A dynamic-appearance audit.
6. Live-gameplay command smoke test.

This package is incremental and requires the prior Classic Administration Editor and Command Runtime Closure updates.
