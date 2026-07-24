# Remaining Systems Pass 0002 — Communications

## Runtime implementation

Implemented `RuntimeCommunicationService` as the owner of channel-policy validation, recipient selection, visibility filtering, ignore filtering, silence/deafness handling, flood control, hidden-sender presentation, and delivery-envelope rendering.

Active channel shells now cover say, whisper, tell, shout, yell, emote, pose, party, guild, global, system, and administrative broadcast. Global communication is disabled by the Classic-safe default policy. Administrative broadcast fails closed for non-administrators.

Party chat delegates membership and connected-recipient routing to `RuntimePartyCoordinationService`; it does not duplicate party ownership. Guild routing accepts projected guild identity but leaves exact guild semantics to the future organization pass.

## Executable validation

`RuntimeCommunicationSmokeReport` verifies:

- same-room local routing
- ignore filtering
- party-channel integration
- guild identity routing
- hidden-sender administrator visibility
- administrative permission denial
- communication-state validation

## Ownership boundaries

- Command parsing and aliases remain owned by the Commands subsystem.
- Party membership remains owned by Party Coordination.
- Guild membership remains owned by Guild/Faction/Organization.
- Identity storage remains owned by Character.
- Terminal transport and host moderation remain at the Worldgroup integration boundary.

## Native reductions

The following remain final-native-pass dependencies: exact shout/yell range, exact command aliases and text, language/color/profanity behavior, invisibility presentation, ignore persistence, logging, moderation, and Worldgroup-host channel boundaries.
