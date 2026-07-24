# Room Description Reference Recovery 0139

## Result

- Canonical room records checked: `26666`
- Native binary-backed descriptions already present: `2417`
- OmniVerse room detail JSON files checked: `1246`
- Reference descriptions promoted: `800`
- Skipped because native binary evidence already exists: `433`
- Skipped due to missing canonical room identity: `8`
- Skipped due to name mismatch: `0`

## Evidence Policy

Native map DAT room descriptions remain higher priority. This pass only fills
rooms that have no native binary-backed description and whose OmniVerse room
detail file carries an explicit `map_id` and `room_number` matching the
active Librarium room catalog. These records are marked
`reference_backed_needs_binary_confirmation`.

## Output

- `Librarium/Objects/room_descriptions/reference_room_description_records_0139.json`
- `Reports/DataDecode/room_description_reference_recovery_0139.json`
