# Code Update 0292 — Pass 22: Gangs / Gang Houses / Casino / Arena / TOP Closure

## Scope

Pass 22 starts from the user's installed Pass8→21 cumulative baseline and overlays the supplied Borin trainer and Custom1 editor updates. The only Pass21/Borin collision, `ServerRuntimeHost.cs`, is merged: Borin keeps its partial-class headless-training integration and Classic keeps the recovered 60-second player autosave interval.

## Native casino closure

- Lucky Strike 1/2: Dragon's Dice and slot/Crowns-and-Skulls command aliases, prices, random tables 998/1020 and payout denominations preserved.
- Lucky Strike 1/10: Dead Jester commands, 100-gold (10000 copper-value) wager and random table 9134 preserved.
- Lucky Strike 1/11: lower-stakes Dice/Slots plus the command-specific Crowns-and-Skulls price and random tables 9136/9137 preserved. Decoded `adddelay 5` is recorded but not executed until its native unit/command-delay semantics are proved.
- Runtime threshold selection is exact for a supplied 1..100 roll. The global RNG source remains UNKNOWN, so `Random.Shared` is an explicitly non-parity temporary roll source and does not promote global RNG.
- Unrecovered outcome prose is not invented; runtime identifies the selected native result textblock and applies only the proven payout.

## Arena

Retained native help proves Practice Arena PvP causes no evil points/life loss and revives a defeated player in Halls Of The Dead with the HP held on last arena entry. Existing live PvP behavior already enforces this contract.

## TOP

TOPTEN adventurers and gang TOP forms are native; class-filtered TOP is operator-confirmed. Retained setup explicitly controls whether experience is included in TOPTEN output. Pass 22 activates experience-descending character/class TOP with NOTOPTEN exclusion as STRONG behavior; tie ordering and exact native output remain unresolved.

## Gangs / gang houses

- CREATE GANG: 100000 XP; max name length 19.
- Promote/Demote remain leader-only.
- Lieutenant invite/uninvite authority is allowed only while the gang leader is offline, matching detailed native help.
- Deed ownership, sundown Silvermere-bank tax, guardians and repossession ownership boundary remain active.
- Post-disband gang-name reuse timing and exact house-content cleanup/reset order stay UNKNOWN.

## Score

PASS 73 / STRONG 73 / UNKNOWN 33 / 179 total. Strict 40.8%; evidence-backed 81.6%.
