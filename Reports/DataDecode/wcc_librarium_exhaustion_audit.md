# CodeUpdate0139 WCC Librarium Exhaustion Audit

This read-only audit inventories the local WCC/WBB evidence currently available to complete Classic Librarium coverage.

It does not mutate `ImportedDatabases/`, generated `/Data`, or Librarium records.

## Room Text Source Gate

- Promotable native WCC map files: WCCMP001.DAT.
- Available reference WCC/WBB map files: WBBMP002.DAT, WCCMP002.DAT.
- Available but not promoted map files: WCCMP002.DAT.
- Missing expected map files: WCCMP003.DAT, WCCMP004.DAT, WCCMP005.DAT, WCCMP006.DAT, WCCMP007.DAT, WCCMP008.DAT, WCCMP009.DAT, WCCMP010.DAT, WCCMP011.DAT, WCCMP012.DAT, WCCMP013.DAT, WCCMP014.DAT, WCCMP015.DAT, WCCMP016.DAT, WCCMP017.DAT.
- Librarium room records: 26,666.
- Librarium room-description records: 2,417.
- Room-description coverage: 9.06%.

## Family Inventory

| Family | Librarium family | Status | NativeRuntime | WCC default | WBB secondary | Librarium records |
| --- | --- | --- | --- | --- | --- | ---: |
| areas | areas | no_local_source | no | no | no | 1 |
| banks | banks | source_present | yes | yes | yes | 27 |
| classes | classes | source_present | yes | yes | yes | 15 |
| gangs | guilds | source_present | yes | yes | yes | 44 |
| items | items | source_present | yes | yes | yes | 1,950 |
| item_world | loot_rules | source_present | no | yes | yes | 1 |
| known_messages | npcs | source_present | yes | yes | yes | 1,100 |
| map_room_text | room_descriptions | source_present | yes | yes | yes | 2,417 |
| messages | events | source_present | yes | yes | yes | 1,501 |
| races | races | source_present | yes | yes | yes | 13 |
| shops | shops | source_present | yes | yes | yes | 178 |
| spells | spells | source_present | yes | yes | yes | 1,350 |
| textblocks | quests | source_present | yes | yes | yes | 2,183 |
| updates | validation_rules | source_present | yes | yes | yes | 1 |
| users | character_foundation | source_present | yes | yes | yes | 1 |

## Interpretation

- Available local WCC/WBB families can continue to be decoded and reconciled into Librarium only when evidence is explicit.
- Room descriptions are currently promoted only from clean promotable native WCC map files.
- Reference-only map files require parser repair or corroborating binary-backed extraction before promotion.
- Maps without local WCCMP files remain blocked; OmniVerse/MegaMud/MudInfo material may corroborate but must not override binary evidence.
