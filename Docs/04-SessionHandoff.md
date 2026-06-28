# Session Handoff

## Current focus
Native MajorMud `.DAT` decoding for standalone Reforged database conversion.

## Latest database version
Database Version 0006 - DAT Decode Correction and Continuation.

## Important correction
Do not assume numbers inside `.DAT` records are counts. Many are skills, abilities, flags, lookup IDs, offsets, bitfields, or references.

## Verified so far
- Native `.DAT` files are cataloged.
- CSV exports are available for cross-checking.
- Several files show strong record-size patterns:
  - `WCCSHOPS.DAT`: 512-byte shop-like slots.
  - `WCCSPELS.DAT`: 256-byte spell-like slots.
  - `WCCITEMS.DAT`: 1024-byte item-like blocks for at least the base item table.
  - `WCCCLASS.DAT`: 153-byte class record pattern after header/padding.
  - `WCCRACE.DAT`: 126-byte race record pattern after header/padding.

## Next recommended task
Fully decode `WCCCLASS.DAT` and `WCCRACE.DAT` first, because they are small and align clearly with CSV exports.
