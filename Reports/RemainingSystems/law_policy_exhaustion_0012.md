# Remaining Systems Update0012 — Law, Bounties, Guards, and Crime Policy

Implemented `RuntimeLawPolicyService` as the evidence-safe owner of crime records, wanted state, bounty policy, crime decay, pardon coordination, and typed law-consequence requests.

## Executable behavior

- Rejects crimes outside explicit law-enforced zones.
- Records deterministic witness sets and severity.
- Creates checked bounty/wanted state.
- Emits guard-alert, fine, jail, pardon, and wanted-state requests without duplicating NPC AI, Character, Economy, PvP, or Rooms formulas.
- Processes explicit crime-decay deadlines.
- Validates state invariants and fails closed on invalid identifiers or arithmetic overflow.
- Adds `--smoke-law-policy`.

## Native dependencies

Exact crime classes, witness/reporting rules, Evil Point coupling, guard behavior, arrest/jail/confiscation formulas, bounty limits, decay, pardon permissions, persistence, and messages remain assigned to the final native pass.
