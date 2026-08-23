# Code Update 0267 - Borin Live NVIDIA Hybrid Composition

## Result

The NVIDIA provider is now part of the production Reforged application composition rather than a test-only backend. Borin's existing `IBrain` consumers use one provider adapter and Hybrid router. The local GGUF brain remains the fallback, personality, memory, and deterministic local reasoning path; NVIDIA does not own Borin state.

## Forge configuration

The existing Forge now exposes:

- Hybrid, LocalOnly, and NvidiaOnly modes.
- The fixed NVIDIA NIM provider and Nemotron 3.5 Lightning 30B A3B model identity.
- A masked API-key indicator.
- Change Key and Test Connection actions.
- Connection and shared 40-RPM-budget status.

The key-entry window uses a `PasswordBox`. The key is written only to Windows Credential Manager and is never rebound into the UI, persisted Forge JSON, or conversation history.

## Routing

Hybrid mode sends conversation, Sysop, engineering, diagnosis, repair, and other complex reasoning to NVIDIA. Routine deterministic gameplay remains local. Provider failures classified as authentication, rate limiting, timeout, network, service/model availability, malformed output, or configuration failure fall back to the local brain when Hybrid fallback is enabled.

NVIDIA results are adapted back into the existing Borin conversation boundary. Conversation consumes structured `speech`; engineering consumes the structured proposed target/change. This preserves the existing Forge and player loops instead of creating parallel chat or coding systems.

## Developer authority

Forge initializes with controlled workspace edits, builds, tests, and local test launches enabled at Validate permission. Main-repository incorporation still requires the user's explicit local approval. Worldsmith mutation remains held.

## Verification

- Reforged.App Release build: PASS, zero errors.
- Borin NVIDIA/provider/authority tests: 32/32 PASS.
- A real NVIDIA request was not sent because no user credential was available to this build session. Use Change Key and Test Connection in the Forge for that proof.
- NuGet vulnerability-feed access produced the existing `NU1900` warning and did not affect compilation.

