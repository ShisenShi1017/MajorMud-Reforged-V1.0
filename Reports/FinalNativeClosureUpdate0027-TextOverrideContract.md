# Final Native Closure Update0027 — HSE/STO Text Override Contract

## Proven closures

- All 194 supplied HSE/STO files are 7-bit ASCII and use LF-only line endings.
- HSE names encode the decimal house ID followed by invariant suffix `15`; payloads contain five or six logical lines, with the first line acting as the title.
- STO payloads contain exactly four logical lines. The first three are description text; the fourth is a canonical WCC filename identity marker, and every STO ends in LF.
- Shared WCC/WBB HSE payloads are byte-identical. STO descriptive payloads are identical across distributions while the WBB copies deliberately preserve the canonical WCC identity marker.

## Runtime rule

Reforged now parses these assets fail-closed and preserves the raw bytes. It does not normalize line endings, transcode non-ASCII input, repair malformed content, or borrow assets across distributions.

## Remaining native dependency

The exact native file-open buffer size, missing-file branch, malformed-file branch, and fallback target still require additional consumer-path evidence.
