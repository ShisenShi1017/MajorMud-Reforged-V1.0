# Code Update 0277 - Borin Autonomous Equipment, Inventory, and Shop Management

## Goal

Allow Borin to act on the progression goals introduced in 0276 by independently managing normal-player equipment, inventory, and shop decisions without turning full autonomy into destructive or random item handling.

## Runtime behavior

- A deterministic equipment/economy plan is built before every autonomous player decision and is also supplied to Borin's reflection cycle.
- Missing/stale inventory or equipment state is refreshed before ownership-changing decisions are trusted.
- Combat/survival remains ahead of routine shopping or inventory cleanup.
- Borin may use the existing ordinary player command surface for LIST, BUY, SELL, GET/TAKE, DROP, GIVE, PUT, EQUIP/WEAR, REMOVE, USE, DRINK, EAT, and related gameplay actions.
- The planner does not send commands and does not expand SYSOP/admin/script/shell authority.
- Purchases are deliberately staged: verify live LIST output, compare the candidate, buy one item, verify INVENTORY, equip/wear deliberately, then verify EQUIPMENT.
- Borin is told not to buy/sell repeatedly just because a name sounds stronger or weaker.
- SELL, DROP, GIVE, PUT, REMOVE, and other irreversible/ownership-changing actions are protected by an evidence rule. Unknown quest items, keys, limited items, upgrade components, or otherwise important-looking items are retained until their role is verified.
- Carried currency is treated conservatively. Zero remains zero-or-not-captured rather than invented purchasing power, and the server remains authoritative for actual price/affordability.

## Librarium equipment/shop guidance

The structured Librarium player advisor now extracts additional read-only item/economy fields from the canonical catalog-listed item records:

- item type
- recorded cost
- weapon minimum/maximum damage
- armor AC/DR bonuses
- accuracy and speed
- worn-on value
- strength requirement

Owned/equipped item names are matched against Librarium item records (including simple `a`/`an`/`the` article normalization) and are surfaced as `ITEM REFERENCE` evidence.

Structured shop records can now surface:

- `SHOP IN CURRENT ROOM` when a shop room reference matches Borin's verified current room
- conservative level-compatible `SHOP CANDIDATE` references for generic/item/weapon-or-armor shops
- service type, recorded level range, and Librarium room reference

A Librarium shop record never proves live stock or price. Borin must use the normal live `LIST` command before purchasing or selling. Shop candidates are destinations/references only and never authorize an invented route.

## Authority order

1. Current verified live server inventory/equipment/shop output.
2. Verified personal item/equipment results from Borin's gameplay history.
3. Proven Librarium item/shop evidence.
4. Strongly-correlated/reference Librarium clues.
5. Borin's own strategy/reflection hypotheses.

## Configuration

`equipmentInventoryPlannerEnabled` defaults to `true` in `AI/Config/borin-player.json`.

## Verification

`BorinCharacterTestHarness` adds deterministic coverage for the new ownership-safety policy. The existing structured Librarium regression fixture is extended with canonical item/shop records and verifies that owned weapon stats and a shop in the current room are exposed as structured evidence.

The repository validator still requires the .NET SDK. The packaging environment used to create this update does not provide `dotnet`, so compilation/runtime execution must be performed on the Windows development machine before merging/releasing.
