# Code Update 0284 — Worldgroup Ownership Scope

## Goal

Remove requirements that are proven to be solely Worldgroup-owned from the MajorMUD parity denominator. Mixed host/module boundaries remain scored only for the MajorMUD-owned behavior.

## Policy

- Worldgroup-only behavior is **out of scope** for MajorMUD parity and receives no PASS/STRONG/UNKNOWN state.
- Mixed requirements are narrowed; only the MajorMUD-owned side remains in the scorecard.
- Nothing is removed merely because it is difficult to prove. Ownership must be explicit enough to separate the host from the MajorMUD module.
- Reforged may still implement host conveniences, but those features do not affect MajorMUD parity scoring.

## Removed from MajorMUD parity

### `worldgroup.semantic-boundary`
This was bookkeeping that measured how far the project had exhausted Worldgroup semantics. It is not a MajorMUD gameplay/runtime requirement.

### `native.worldgroup-integration`
The retained dependency questions are terminal-session APIs, account/billing/access interfaces, host scheduler/shutdown hooks, host message ownership, and host/library call boundaries. Those are host integration concerns, not MajorMUD game behavior.

## Mixed requirements narrowed

The following remain UNKNOWN, but their Worldgroup-only questions no longer count as required MajorMUD closure:

- administrative-policy
- security-permissions
- logging-diagnostics
- startup-shutdown
- account-session-ownership
- world-rules-configuration
- help-reference
- reforged-extension-boundaries

Examples of removed host-only subquestions include Worldgroup administrator levels, authentication/account structures, module admission, host shutdown hooks, account bans, module-access restrictions, maximum board users, and general Worldgroup policy.

MajorMUD-owned behavior such as character disconnect cleanup, module save ordering, WCCMMUD.INI command-key handoff, in-game command authorization, and game-rule configuration remains in scope.

## Evidence basis

- Worldgroup setup documentation installs MajorMUD as a module/page under the Worldgroup server and performs security/accounting configuration outside the game module.
- WCCMMUD.INI documentation and operator reports show Worldgroup security keys being handed to MajorMUD `SYS_*_KEY` settings.
- Official MajorMUD command help separately documents in-realm commands, character saving on game exit, parties, gangs, communication, shops, banks, combat and other module-owned behavior.

## Score effect

Pass 13 generated scorecard: PASS 69 / STRONG 56 / UNKNOWN 48 / TOTAL 173.

Pass 14 ownership-scoped scorecard: PASS 69 / STRONG 55 / UNKNOWN 47 / TOTAL 171.

No MajorMUD behavior was promoted merely to improve the percentage. Two non-MajorMUD requirements were removed from the denominator.
