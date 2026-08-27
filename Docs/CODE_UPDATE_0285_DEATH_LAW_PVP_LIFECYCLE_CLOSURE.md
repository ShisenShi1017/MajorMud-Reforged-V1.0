# Code Update 0285 — Death / Law / PvP / Character Lifecycle Closure

## Scope

Pass 15 closes independently recoverable Classic MajorMUD death/law/PvP/lifecycle rules without guessing the remaining native boundaries.

### Activated exact behavior

- MajorMUD graceful `EXIT`/`X` waits five seconds before the Telnet realm session closes; the existing session finalizer saves the persistent character.
- `REST` retains the already-decoded 20-second uninterrupted activation delay and interruption on movement/combat actions.

### Activated evidence-backed behavior

- Lawful-target attacks multiply the applicable attack Evil Point penalty by three.
- Outlaw/Criminal/Villain/Fiend law consequence classification is centralized; exact guard/bounty spawn cadence remains unresolved.
- 300+ Evil Points still prevents initiating another attack.
- Per-realm PvP configuration now persists and enforces the recovered maximum-level-difference setting.
- Decoded `Arena Practice Room` entry captures the player's HP. PvP in those rooms awards no attack Evil Points; practice-arena defeat consumes no life/corpse transition and revives in Halls Of The Dead (map 1 / room 2189) with the captured entry HP.
- If practice-arena entry HP was not captured, the arena death path fails closed rather than consuming a life or inventing a restoration value.

### Worldgroup exclusion

Raw reconnect grace/socket takeover is marked `EXCLUDED_WORLDGROUP`. Retained import/callsite evidence assigns raw session/user state to Worldgroup host services. MajorMUD-owned effects after a disconnect (combat retention, party succession, trade escrow, offline corpse handling) remain unresolved.

## Explicitly unresolved

- Exact normal-death XP loss formula, rounding, caps and application order.
- Deathpile floor-capacity overflow ordering and all deathpile exceptions not already independently proven.
- Exact guard/bounty spawn cadence, arrest/jail/fine/confiscation ordering, witness rules and law persistence.
- Remaining PvP consent/safe-zone/kill-credit/loot/cooldown/logout semantics.
- Inn/sleep/safe-logout/login-room rules beyond the proven REST contract.
- MajorMUD linkdead combat, party leadership, trade escrow, and offline corpse reconciliation.

## Scorecard

Update 0285 reconciles the assertion ledger with the public scorecard and adds five independently scored subrules:

- PASS 71
- STRONG 58
- UNKNOWN 47
- Total 176
- Strict parity 40.3%
- Evidence-backed parity 73.3%

The UNKNOWN count is intentionally unchanged: this pass decomposes broad blockers and credits proven subrules without falsely claiming their residual native boundaries are solved.
