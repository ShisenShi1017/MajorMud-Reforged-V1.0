# Combat & Spells Runtime Update 0174

Implemented the pursuit, opportunity-attack, and failed-flee consumption boundary.

## Runtime behavior

- Failed escapes consume a promoted round/resource cost only when explicit evidence policy is present.
- Opportunity attacks execute in canonical attacker order before the room-exit handoff.
- A lethal opportunity attack suppresses movement and pursuit.
- Successful movement delegates cleanup to the existing movement coordinator.
- Pursuer handoff is deterministic and bounded by promoted policy.
- Native pursuit selection and coefficients remain fail-closed.
