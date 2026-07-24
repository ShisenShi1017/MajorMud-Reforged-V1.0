# Worldgroup Special-Command Keyword and Remoteaction Closure — Update0021

## Proven findings

- The MajorMUD special-command interpreter recognizes 41 ordered keywords.
- `text` parses one textblock identifier and calls `0x0043BCCA(textblockId, 0)`.
- `roomtext` parses one textblock identifier and calls `0x0043BE15(roomY, roomX, textblockId, 0)` using player room coordinates at `+0xC8/+0xC4`.
- `remoteaction` parses four integers, requires its mode to be in `0..10`, resolves the current room/action record, and calls `0x0046C573(record, actionIndex, selector, mode)`.
- `0x0046C573` is a MajorMUD-local room action executor. It emits optional local/room text, selects behavior from the action type word at `record + 0x360 + actionIndex*2`, and then enters type-specific state mutation/dispatch.
- Result `2` is a terminal rejection for the current colon chain: the interpreter restores the delimiter when needed, clears the remaining-line pointer, and returns through the common epilogue.

## Boundary

The complete type-specific remoteaction switch remains to be decoded. No semantic names are assigned to numeric action types without independent evidence.
