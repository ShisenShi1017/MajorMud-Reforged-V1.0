# Combat & Spells Runtime Update 0176

## Executable advance

Implemented an evidence-gated resurrection and revival coordinator. The runtime now validates canonical targets, source definitions, rooms and corpses; requires a dead and eligible target; rejects expired eligibility; computes bounded HP, mana and energy restoration; explicitly keeps the prior combat state terminated; and emits restoration, status, movement, persistence and message requests through an atomic sink protocol.

## Execution order

1. Validate eligibility.
2. Consume the promoted resource cost.
3. Clear death state.
4. Restore HP.
5. Restore mana.
6. Restore energy.
7. Apply active-effect disposition.
8. Move to the promoted destination.
9. Apply resurrection sickness when the policy requests it.
10. Persist character state.
11. Emit the revival event.

All sinks prepare before any commit. Partial commits rollback in reverse order.

## Safety boundary

The runtime does not infer production eligibility windows, coefficients, corpse rules, experience recovery, sickness, or resource timing. Missing or weak policy data fails closed. Resurrection never silently recreates the prior combat instance.
