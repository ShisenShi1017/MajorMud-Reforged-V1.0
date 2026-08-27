# Code Update 0291 — Pass 21 Settings / Healers / Access / Rankings Closure

Pass 21 reconciles the active MajorMUD 1.11p parity target with the operator-confirmed gameplay boundary and closes several false Worldgroup/host requirements without inventing native behavior.

## Parity-scope corrections

The active audit now excludes requirements that are intentionally outside MajorMUD gameplay parity: Worldgroup mail/offline messaging, account/session/login behavior, host user/line/resource limits, weather/day-night, native logging/diagnostics, native SYSOP command-interface parity, and startup/crash/restart behavior. Reforged can provide its own account/session and editor administration model.

MajorMUD-owned in-world behavior is still audited where it affects gameplay: ordinary saving, character departure effects, world state, party/combat cleanup, gameplay data limits, and player-visible action failures.

## Native SET behavior

Bare native SET settings toggle their current state. `SET RECEIVE` now toggles RECEIVE instead of merely reporting it. Retained native help independently proves toggle forms for GOSSIP and AUCTION and also proves their explicit ON/OFF forms, so those explicit forms remain supported. FOLLOW BLIND/NORMAL and SPEECH FAST/SLOW remain value settings because the native help directly documents those values.

The retained SET help has conflicting wording around TELEPATH/RECEIVE polarity. Pass 21 therefore does not invent an explicit TELEPATH polarity; the bare toggle is active while unresolved explicit polarity remains evidence-gated.

## Healer shops

Native help directly states that `BUY HEALING` in the appropriate room heals all wounds for a price. The generated Temple/Healer service is therefore recognized as a healer transaction rather than as an ordinary inventory-item purchase.

No universal healer price is invented. The corpus also contains room-owned textblocks with exact commands such as `buy minor healing`, `buy major healing`, cures, explicit `price`, `cast`, and `adddelay` operations. Those decoded room scripts are preserved as their own evidence/authority. The generic ShopType 5 price/debit/order rule remains UNKNOWN until recovered.

## Tickets, passes, and movement

Classic has no persistent transportation-service model. Pass 21 adds the decoded native `Ticket/Item` exit family as a required-item gate. The retained example `exit.map2.2552.u` moves from The Drunken Demon Inn, Stairwell to room 2/2554 and requires item 924.

The gate does not consume the ticket because no explicit native consumption evidence is present. Exact per-gate consumption, failure messages, pass semantics, and additional command-triggered relocations remain UNKNOWN.

## TOP rankings

The old live route treated all `TOP` requests as gang rankings. Pass 21 separates the native surfaces:

- `TOPTEN` / adventurer TOP
- class-filtered TOP forms such as operator-confirmed `TOP 10 WITCHHUNTER`
- `TOPTEN GANGS` / `TOP <n> GANGS`

Native help proves TOPTEN adventurers and gang TOP lists, and retained configuration data proves TOPTEN list controls. Exact adventurer/class ranking metric, tie order, refresh behavior and output are not invented; unresolved character ranking requests are recognized and fail closed at that exact boundary.

## Gangs and gang houses

The Classic organization scope is now closed as gangs only. Native help proves CREATE GANG, member invitation/removal, BROADGANG/BG, gang TOP lists, LEAVE GANG, DISBAND, PROMOTE and DEMOTE.

Gang-house help further proves purchase by the gang leader at the Deed Shoppe, tax collection every sundown, debit from the deed holder's Silvermere bank account, and repossession with contents when funds are insufficient. Remaining exact deed/upkeep values and house-specific behavior stay UNKNOWN.

## Casino and arena

Casino and arena existence are no longer treated as optional/possibly-Worldgroup behavior. They are native MajorMUD activities. Exact casino games/bets/odds/payouts and remaining arena mechanics still require recovery. The already recovered practice-arena death exception remains separate evidence-backed behavior.

## Active score

- PASS: 73
- STRONG: 70
- UNKNOWN: 36
- Total: 179
- Strict parity: 40.8%
- Evidence-backed parity: 79.9%

This score change is a scope correction plus the gangs-only organization promotion; Pass 21 does not award PASS/STRONG credit merely for recognizing an unresolved command.
