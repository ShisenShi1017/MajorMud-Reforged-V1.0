# Code Update 0262 — Borin Provider Context and Security Boundary

## Outcome

Borin now has a provider-neutral cloud-context boundary. NVIDIA NIM no longer assembles prompts directly from the full `BrainContext`; it consumes a compact `BorinProviderContext` produced locally by `BorinContextBuilder`.

## Authority and privacy rules

- `BrainContext` and all local memory/session stores remain authoritative.
- Verified connection, authentication, terminal phase, prompt freshness, room, exits, vitals, activity, inventory, equipment, and command ownership are labeled as live state.
- Durable and episodic memories are labeled separately and cannot masquerade as live perception.
- Only already-retrieved relevant memories are eligible; counts and per-entry lengths are bounded.
- Absolute repository roots and memory storage paths are not sent.
- Credential-shaped content is redacted before transport.
- The context package grants no tools and performs no actions.
- Capability names reflect current local role/permission settings but remain descriptive until the structured tool phase.

## Roles

The context builder classifies the current purpose into capability sets over the same Borin identity:

- Player
- Sysop
- Developer

Roles may be combined. No duplicate Borin implementation is created.

## State fingerprint

Each provider context contains a SHA-256 fingerprint over the captured game-state facts, including capture time, connection/login/phase, world, character, room, exits, vitals, activity, and last-command ownership.

This fingerprint is not yet an execution token. It provides the request-side identity needed by the later structured-action and stale-state validation phases. Autonomous execution remains disabled.

## Files

- `Apps/Source/Reforged.App/Brains/BorinContextBuilder.cs`
- `Apps/Source/Reforged.App/Brains/NvidiaNimProvider.cs`
- `Apps/Source/Borin.AiProviderTests/Program.cs`

## Verification

- AI provider/context tests: 10/10 pass.
- Existing Borin character tests: run as the final regression gate for this bundle.
- Release solution build: run as the final build gate for this bundle.

No live NVIDIA request was made. No key was requested or exposed. Provider routing, rate limiting, structured actions, validator integration, UI controls, and autonomous cloud use remain disabled.
