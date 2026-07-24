# Room Native Effect Linkage and Message Decode

- Gap exit records resolved: 12
- Distinct native/passive effects resolved: 7
- MegaMUD `/mp` route command findings for these gaps: 0
- Native WCCMSG message records cataloged: 117

## Resolution

The remaining hidden-action gaps are classified as native/passive room effects. MMExplorer exposes the hidden Needs Actions qualifier, but no decoded Action row is available. MegaMUD `/mp` route replay did not produce a map-qualified command traversal for these exact exits, so no player command was invented or promoted.

## Message ID Policy

Message IDs use the native WCCMSG byte offset (`wccmsg.offset.N`) plus 512-byte block location. This is the safest proven identifier available in the current repository; no internal enum was guessed.
- room_action_command_alias: 31
- room_action_success: 9
- door_open: 18
- hidden_exit_open: 20
- hidden_exit_traverse: 8
- lock_or_key_failure: 8
- search_success: 7
- generic_failure: 19
