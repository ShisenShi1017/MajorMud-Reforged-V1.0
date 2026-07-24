# Remaining Systems Pass 0001 — Party Coordination

## Executable advancement

`RuntimePartyCoordinationService` now owns a deterministic, fail-closed party lifecycle: creation, invitation, acceptance, refusal, leaving, kicking, leadership transfer, connected-state routing, party-chat recipient resolution, and follower movement request generation.

The service deliberately does not own combat, reward, quest, spell, economy, or movement formulas. It emits coordination decisions for those authoritative subsystems.

## Validation coverage

The smoke command `--smoke-party-coordination` verifies invitation acceptance and expiration, co-located follow routing, connected-only party chat, leadership transfer, kicking, offline handling, and state-index consistency.

## Evidence disposition

Leader/member uniqueness and party coordination are promoted as strongly correlated from the existing multiplayer runtime and smoke fixtures. Exact Classic limits, timeout values, persistence, messages, reward sharing, group spell behavior, and logout rules remain explicit final-native dependencies.

## Subsystem status

Party coordination is **partially exhausted**. The executable lifecycle and safe boundaries are active. Native constants and cross-subsystem Classic formulas remain blocked from promotion until their source behavior is recovered.
