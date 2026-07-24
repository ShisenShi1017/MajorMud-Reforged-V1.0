# Transport Services Exhaustion — Update0014

Implemented deterministic boarding, capacity, fare requests, departure, in-transit restrictions, arrival routing, disconnect coordination, and state validation.

## Ownership

Transportation owns trip lifecycle and passenger coordination. Economy owns fare mutation, Rooms owns movement, and session lifecycle owns disconnect handling.

## Native dependencies

Exact Classic record families, schedules, messages, refunds, party boarding, persistence, and restart recovery remain for the final native pass.
