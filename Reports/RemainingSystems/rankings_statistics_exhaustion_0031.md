# Rankings and Statistics Exhaustion — Update0031

Implemented a deterministic, owner-routed rankings projection for Reforged. Source subsystems retain metric truth; this service validates updates, applies visibility, computes stable ties, supports explicit resets, and persists only ranking snapshots.

## Executable behavior

- Owner-authorized set and checked increment operations
- Public, participant-only, and administrator-only views
- Hidden-subject and administrator-exclusion policy
- Stable ascending or descending ordering
- Competition ranking with deterministic ties
- Manual reset authorization and reset metadata
- Atomic schema-versioned snapshot persistence
- Smoke command: `--smoke-rankings-statistics`

## Ownership

Character, Economy, Combat, Quests, PvP, and other source systems own the underlying values. Rankings owns only validated projection, visibility, ordering, reset policy, and snapshot lifecycle. Clock scheduling may request timed resets; Save Coordination owns persistence orchestration.

## Native dependencies

The final native pass must determine whether MajorMUD 1.11p exposes rankings, which metrics exist, exact update timing, tie behavior, privacy rules, reset schedules, administrator exclusion, persistence, and messages. Unsupported achievement behavior remains disabled.
