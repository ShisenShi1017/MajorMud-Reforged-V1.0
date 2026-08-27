# Code Update 0293 — Pass 23 Healers / Access / Room Commands Closure

## Scope

Pass 23 closes two previously broad UNKNOWN boundaries without inventing a transport system or a universal healer formula:

1. native healer-room purchases recovered from room-owned textblock action scripts; and
2. ticket/pass access plus immediate command-triggered room relocation.

The pass also re-audited party XP/loot/help data. No exact native party XP/loot split formula was recovered, so party coordination remains UNKNOWN.

## Native healer recovery

Native HELP states that `BUY HEALING` in an appropriate room heals all wounds for a price. The decoded textblock graph contains room-owned healer command lines with the exact operation sequence:

`monsters -> price <amount> <failure-message> -> cast <spell> -> adddelay <value>`

Pass 23 found 132 such room-owned command lines across textblocks 159, 1602-1606, 1608-1611 and 4351, using healer spells 219-224:

- 219 — minor healing
- 220 — major healing
- 221 — greater healing
- 222 — cure poison
- 223 — cure disease
- 224 — remove curse

The `price` values are interpreted through the existing copper-value denomination authority. This is supported independently by the Pass 22 casino correlation where decoded `price 10000` is the native 100-gold Dead Jester wager and one gold equals 100 copper units.

Message 318 is recovered exactly as `You cannot afford that service.` and is returned when the character cannot pay the decoded healer price.

The runtime checks healer/NPC presence, verifies affordability before mutation, applies the existing spell/cure/remove-curse authority, then debits the exact decoded price. Character and live-spell state are rolled back together if the transaction fails.

### Deliberate gate

The meaning/time unit of `adddelay` is not yet proven. Pass 23 recognizes the decoded delay operand but does not invent a seconds/rounds conversion. Generic ShopType 5 `BUY HEALING` pricing outside a decoded scripted price/cast line also remains unresolved.

## Ticket/pass and room-command movement

Classic MajorMUD is not modeled as a transportation service. Native travel/access consists of inventory-gated exits and room actions that can immediately move the character.

The existing native type-17 Ticket/Item exit gate continues to require possession of the decoded item. Tickets/passes are not consumed unless native evidence proves consumption for that access path.

Pass 23 adds room-owned action-script resolution by exact source map/room plus exact entered command. It found 45 room-owned trigger lines across 13 textblocks whose entire matched line contains only native `message`/`text` presentation and one `teleport`. Those lines are now executable.

Examples include:

- room 2/1306 `GO/ENTER/CRAWL/CLIMB HOLE` -> map 2 room 487, messages 767/768;
- room 6/1354 `GO/ENTER OPENING/HOLE` -> map 6 room 1359, messages 1573/1574;
- room 10/235 `READ/LOOK BOOK` -> map 15 room 740, messages 2585/2950;
- room 15/772 `TOUCH/MOVE/PUSH CRYSTAL` -> map 15 room 773;
- room 17/1742 `LOVE` / `SAY LOVE` -> map 17 room 1743.

After the decoded teleport the session location is saved and room-entry NPC respawn admission runs, matching the Pass 20 room-entry spawn boundary.

Complex scripts containing unresolved delays or additional condition/effect opcodes remain evidence-gated rather than being generalized from the simple subset.

## Runtime files

- `Apps/Source/Reforged.Server/RuntimeClassicRoomActionScripts.cs` — new evidence-gated healer and immediate room-action executor.
- `Apps/Source/Reforged.Server/RuntimeLivingWorld.cs` — dispatch hook before generic room-object fallback.
- `Apps/Source/Reforged.Server/RuntimeClassicTextblockGraphAuthority.cs` — source-map/room action-plan resolution.
- `Apps/Source/Reforged.Server/RuntimeClassicNativeTextblockPayloadAuthority.cs` — authoritative Librarium payload fallback when generated `Data/Classic` payload projections are absent.
- `Apps/Source/Reforged.Server/RuntimeMajorMudParityAudit.cs` — current ledger pointer 0293.

## Parity result

Two UNKNOWN requirements move to STRONG:

- `economy.non-item-service-cost-order`
- `native.paid-room-transfers` (legacy identifier retained; description now explicitly means ticket/pass gates and immediate room-command relocation, not a transport service)

Score after Pass 23:

- PASS 73
- STRONG 75
- UNKNOWN 31
- Total 179
- Strict parity 40.8%
- Evidence-backed parity 82.7%

No unresolved mechanic was promoted to PASS.
