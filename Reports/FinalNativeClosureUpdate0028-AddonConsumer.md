# Final Native Closure Update0028 — ADDON.SYS Consumer

## Proven closure

- WCC and WBB use byte-identical loader and lookup code, differing only in the distribution-prefixed filename literal.
- The file is opened with `fopen(..., "rt")`.
- Lines are read through `fgets` into a 100-byte buffer.
- Each accepted record becomes a 12-byte node: slot, token pointer, next pointer.
- The slot is parsed with `atol`; the first colon separates the token. CR or LF terminates the copied token.
- Nodes are prepended, so a later duplicate slot shadows an earlier record.
- Lookup is lazy, linear, and returns null when the file is absent or the slot is unknown.
- A malformed line without a colon is discarded without fabricating behavior.
- Token comparison calls the Worldgroup `_sameas` service; exact collation remains host-owned and is not emulated here.

## Remaining boundary

This closes the MajorMUD-side loading and lookup contract. Token generation or authorization remains outside this path.
