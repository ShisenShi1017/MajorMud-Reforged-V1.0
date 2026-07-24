# Guardian Hall Phase 3-0156 — Worldsmith Approval and Apply

Worldsmith proposals now use an explicit two-step approval gate before isolated staging.

## Workflow

1. Inspect a Test, Development, or Offline Projection realm.
2. Generate a proposal. The proposal contains a generated approval code.
3. Enter the displayed code and approve the exact proposal. Approval records the proposal SHA-256.
4. Apply the approved proposal. The service verifies the safe target, approval requirement, and proposal identity.
5. The proposal is copied only into `GuardianHall/Worldsmith/Applied/<realm>/<project>` with an apply receipt and rollback backup.

## Safety

- Live Realm remains blocked.
- Approval cannot proceed without a matching code.
- Apply cannot proceed without a matching approved proposal.
- Existing staged proposal copies are backed up before replacement.
- Apply does not mutate generated runtime data or Librarium content.
- Every apply creates a JSON receipt containing source hash, target, paths, and mutation flags.
