# OmniVerse Reference Audit Policy

## Purpose

OmniVerse can help Reforged compare current game-reference data against an active MajorMUD realm, but it is not the Reforged source of truth.

Reforged `Data/` remains canonical. `ImportedDatabases/` remains archive/reference material. OmniVerse may identify possible corrections only after a reviewed audit.

## Verified Public Source

- Site: `https://omniversebbs.com/`
- Current MajorMUD world id: `wcc`
- Related world ids observed: `wbb` for HellaMUD and `wdd` for OmniMUD

Verified public endpoints during CodeUpdate-0034 discovery:

- `/api/omni/world-status`
- `/api/omni/codex?type=item&world=wcc&limit=3`
- `/api/omni/codex?type=spell&world=wcc&limit=3`
- `/api/live/shops?world=wcc`

Additional public endpoints found in the website bundle that may be useful later:

- `/api/omni/monsters/{world}`
- `/api/omni/bosses/{world}`
- `/api/omni/monster/{id}/{world}`
- `/api/omni/room/{world}/{map}/{room}`
- `/api/omni/roommap/{world}/{map}/{room}?radius={radius}`
- `/api/omni/search?q={query}`
- `/api/live/shop/{world}/{shopNumber}`
- `/api/live/item-shops/{world}/{itemNumber}`
- `/api/live/item-holders/{world}/{itemNumber}`

## Audit Rules

- Do not import OmniVerse data directly into `Data/`.
- Do not overwrite `ImportedDatabases/`.
- Do not treat live player, live monster, or online-state data as canonical game design data.
- Use OmniVerse for comparison of names, legacy numbers, category labels, shop/trainer placement, room links, and obvious field mismatches.
- Every proposed correction must include source field, current Reforged field, difference, confidence, and approval status.
- Approved corrections should be made in a separate CodeUpdate or DatabaseVersion after review.

## Suggested Future Audit Categories

- Items: compare legacy number, name, type, cost, restrictions, requirements, and descriptions.
- Spells: compare legacy number, name, level, cost, targeting, effect ranges, and abilities.
- Shops/trainers: compare shop number, room number, trainer class, level range, markup, and stocked item counts.
- Monsters: compare template number, name, HP, XP, spawn rooms, boss/NPC flags, and carried items where stable.
- Rooms: compare map id, room number, room name, exits, and shop/trainer references.

## Non-Goals

- No data rewrite.
- No gameplay implementation.
- No live editor implementation.
- No dependency on OmniVerse for normal runtime.
- No GitHub Actions network dependency.
