# Session Handoff - After Database Version 0007

## Current Focus
Native MajorMud `.DAT` decoding for standalone Reforged `Data/` generation.

## Completed This Version
- Continued binary/string analysis for all native `.DAT` files.
- Produced stricter reports separating verified facts from hypotheses.
- Reaffirmed that numeric fields may be skills/references/flags/offsets and must not be named prematurely.
- Added improved importer probe tooling.

## Best Current Decode Targets
1. `WCCSHOPS.DAT` - strongest 512-byte slot evidence.
2. `WCCCLASS.DAT` - plausible 153-byte class slots.
3. `WCCSPELS.DAT` - plausible 256-byte indexed slots.
4. `WCCITEMS.DAT` - plausible 1024-byte indexed blocks.

## Next Recommended Task
Build a shop-record overlay by comparing each 512-byte shop slot against the CSV shop columns.
