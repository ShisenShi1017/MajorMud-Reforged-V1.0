# Help and In-Game Reference Exhaustion — Update0030

## Implemented

- Central validated help-topic and alias catalog.
- Deterministic exact resolution and permission-filtered search.
- Player, builder, administrator, and host visibility boundaries.
- Explicit module, host, generated-catalog, and Librarium content ownership.
- Related-topic and source-textblock relationships.
- Canonical rendering and malformed-query rejection.
- Executable `--smoke-help-reference` coverage.

## Ownership

The help subsystem owns lookup, search, visibility, and presentation coordination. Commands, Abilities, Spells, Items, Quests, Textblocks, Security, Worldgroup, and Librarium retain ownership of their source content and permissions.

## Native dependencies

Exact MajorMUD 1.11p help records, aliases, pagination, formatting, matching, textblock relationships, and Worldgroup boundary remain for the final native pass. No native help content or commands were invented.
