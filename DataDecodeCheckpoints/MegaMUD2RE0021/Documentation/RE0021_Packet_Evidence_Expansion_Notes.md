# Packet Evidence Expansion Notes — RE0021

## Current interpretation
The strongest evidence still points to a Telnet/text command workflow. RE0021 adds route-script command evidence from `.mp` files: route rows bind state/hash-like fields to emitted command text.

## Evidence-backed command classes

| Class | Evidence | Status |
|---|---|---|
| BBS login prompts/replies | BBS.INI `LogonPromptN` / `LogonReplyN` | Verified profile behavior |
| Telnet connection lifecycle | MegaMud.TRC events | Verified runtime trace behavior |
| Movement command tokens | `.mp` rows with `n/s/e/w/ne/nw/se/sw/u/d` | Verified script command text |
| Literal action commands | `.mp` rows such as `@party ...` | Verified script command text |
| Raw binary packet layout | None in inspected evidence | Unverified |

## Implementation guidance
Model the current protocol layer as command-text over a Telnet-like session until raw packet captures prove otherwise. Store unknown pre-command `.mp` fields as opaque route/node/state fields.

## Do not promote
Do not create packet opcodes or binary field specs from the `.mp` prefix fields. They may be route/node/state identifiers rather than network packet bytes.
