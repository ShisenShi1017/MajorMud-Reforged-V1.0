# Final Native Closure Update0030 — BIN Screen Load and Normalization

## Proven

The supplied WCC utility opens each screen asset in binary mode (`rb`) and requires one complete 4,000-byte `fread`. The buffer is interpreted as 2,000 two-byte character/attribute cells, matching an 80×25 text screen. Open failure and short-read failure enter explicit native error paths, and an opened stream is closed after the read attempt.

The native normalizer at `0x00409F0C` walks exactly 2,000 cells with a two-byte stride. Each attribute byte is passed through the utility's native attribute mapper. When attribute bit `0x08` is clear, character bytes `0xDC` and `0xDF` are replaced by ASCII space (`0x20`). Other character bytes are preserved. Caller-provided buffers are normalized in place; a null buffer selects the utility's internal 4,000-byte screen buffer.

## Not claimed

This pass does not assign a speculative palette name to the internal attribute mapper and does not yet recover the final physical console-write ordering.
