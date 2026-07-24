# Code Update 0186 — Character Creation Recovery and Gender

## Runtime corrections

- Character creation now prompts for **Male** or **Female** before race and class selection.
- A validation or runtime creation failure no longer closes the Telnet socket. The exact error is displayed and the player is returned to character creation.
- Persisted character gender is copied into the live session actor.
- `score` displays character gender.
- `examine <character>` / `look <character>` reports level, gender, race, and class.
- Existing saves with no gender remain compatible and display `Unspecified`.

## MegaMUD symptom addressed

MegaMUD appeared to hang because the Host closed the connection immediately after a creation exception. Update 0186 keeps the connection open, allowing MegaMUD to continue receiving prompts and allowing the Host error to be diagnosed.

## Deployment requirement

The Host must be rebuilt after overlay installation. Running an older prebuilt executable will not contain the prompt or recovery behavior even when the source tree does.
