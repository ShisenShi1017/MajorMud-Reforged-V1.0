# Final Native Closure Update0031 — BIN Attribute Mapping and Console Write Ordering

## Proven

The WCC utility attribute mapper at `0x0040E9B5` has two exact byte-transform modes. When the mode word at `0x0041703C` is zero, attributes whose `0x70` field is fully set collapse to `0x70`; every other attribute becomes `(attribute & 0x88) | 0x07`. When the mode word is nonzero, the mapper clears bit `0x80` and preserves bits `0x00–0x7F`.

The writer at `0x0040FABA` rounds the starting byte offset down to an even boundary, converts it to an 80-column `(x,y)` cell coordinate, and derives the cell count as `byte_count >> 1`. It stages characters and attributes in ascending cell order. Attributes are zero-extended from 8-bit bytes to Win32 `WORD` entries.

The physical API sequence is exact: `WriteConsoleOutputCharacterA` is called first, followed by `WriteConsoleOutputAttribute`, using the same console handle, coordinate, and cell count. The recovered code does not test either API return value. If the console-enabled word at `0x00417070` is zero, no physical write call occurs.

## Not claimed

This pass does not assign historical color names to the transform modes and does not claim the two Win32 calls are physically atomic.
