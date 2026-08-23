# Code Update 0261 - Borin Omniverse Intent Scope Repair

## Purpose

Prevents the word `Omniverse` by itself from forcing Forge into the direct server/connection-status answer path.

## Behavior

Connection/server state is answered directly only for explicit questions such as:

- Are you connected to Omniverse?
- What server are you playing on?
- What is your connection status?

Unrelated questions remain in their real intent, including:

- What are the current exits on Omniverse?
- What monsters have you seen on Omniverse?
- How do you buy a club on Omniverse?
- What have you learned about Omniverse?

All verified-state, active-perception, autonomous-play, real-terminal routing, NO_REPLY suppression, and 270-character boundaries remain unchanged.
