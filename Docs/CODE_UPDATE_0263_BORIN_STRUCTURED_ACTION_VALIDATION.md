# Code Update 0263 — Borin Structured Actions and Local Validation

## Outcome

NVIDIA NIM responses are now constrained to a strict `borin_decision` JSON schema and parsed into `BorinAiDecision`. Provider prose is no longer accepted as an actionable NVIDIA result.

The local `BorinActionValidator` validates proposals but has no terminal, filesystem, editor, process, or world executor. A successful validation result remains only a proposal for a later controlled translator/gateway phase.

## Decision contract

Every decision requires exactly:

- `speech`: string or null
- `intent`: one known `BorinAiIntent`
- `target`: string or null
- `confidence`: finite number from 0 through 1
- `reason`: nonempty bounded string
- `stateFingerprint`: the exact request-state fingerprint

Unknown properties, unknown intents, missing properties, incorrect JSON types, invalid confidence, empty non-action speech, and malformed JSON fail closed.

## Intent families

- Player: look, move, inventory, attack, cast, interact
- Sysop: inspect room/exit/monster/item/script/trigger and report issue
- Developer: search/read/reference, propose/apply patch, build/test, test-server lifecycle, runtime inspection, comparison, and issue lifecycle
- Non-action: none or speech

These are capability names, not raw commands. No arbitrary terminal or shell intent exists.

## Validator checks

- Returned fingerprint must equal the state sent to the provider.
- The request fingerprint must still equal a newly built fingerprint of current authoritative state.
- Role must contain the intent family.
- Forge permission and feature switches must permit development actions.
- Player actions require a connected, logged-in session with a verified prompt.
- Movement requires a canonical direction present in currently verified exits.
- Targeted actions require a nonempty target.
- Multiline and terminal-control target text is blocked.
- The same action for the same state is rejected during the duplicate window.

Existing command, editor, engineering workspace, Test Realm, and production controls remain downstream authorities and are not bypassed.

## Files

- `Apps/Source/Reforged.App/Brains/BorinStructuredActions.cs`
- `Apps/Source/Reforged.App/Brains/AiProviderContracts.cs`
- `Apps/Source/Reforged.App/Brains/NvidiaNimProvider.cs`
- `Apps/Source/Borin.AiProviderTests/Program.cs`

## Verification

- AI provider/context/action tests: 16/16 pass.
- Existing Borin character tests and the full Release solution build are the final regression gates for this bundle.

No provider is active in the Forge UI, no live NVIDIA request was made, and no model response was executed.
