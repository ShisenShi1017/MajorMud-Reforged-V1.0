# Native DAT Decode Status

Database Version 0004 starts native `.DAT` decoding. This is a decode foundation, not a claim that every binary field is fully understood.

## Confirmed source files analyzed

| File | Size | SHA-256 prefix | String count | Strong block clues |
|---|---:|---|---:|---|
| `WCCACMSR.DAT` | 6,144 | `ce9790c17903` | 0 | 128x48, 256x24, 512x12, 1024x6 |
| `WCCACTS.DAT` | 65,536 | `baf14617411f` | 1,309 | 128x512, 256x256, 512x128, 1024x64 |
| `WCCBANKS.DAT` | 1,536 | `58b3fa431854` | 0 | 128x12, 256x6, 512x3 |
| `WCCCLASS.DAT` | 6,144 | `8cc91e569b45` | 30 | 128x48, 256x24, 512x12, 1024x6 |
| `WCCGANGS.DAT` | 2,048 | `19ab7942a50e` | 0 | 128x16, 256x8, 512x4, 1024x2 |
| `WCCITEMS.DAT` | 1,404,928 | `22d54b7aab28` | 11,627 | 128x10976, 256x5488, 512x2744, 1024x1372 |
| `WCCKNMSR.DAT` | 561,152 | `b33f96d83705` | 5,656 | 128x4384, 256x2192, 512x1096, 1024x548 |
| `WCCMP001.DAT` | 30,973,440 | `17442fe74d60` | 169,130 | 128x241980, 256x120990, 512x60495 |
| `WCCMSG.DAT` | 870,912 | `37546e9bdc91` | 16,363 | 128x6804, 256x3402, 512x1701 |
| `WCCRACE.DAT` | 3,072 | `1c227140da2f` | 25 | 128x24, 256x12, 512x6, 1024x3 |
| `WCCSHOPS.DAT` | 78,336 | `1d5d8bd67671` | 762 | 128x612, 153x512, 256x306, 512x153 |
| `WCCSPELS.DAT` | 256,512 | `131d9221f12e` | 5,425 | 128x2004, 256x1002, 512x501 |
| `WCCTEXT.DAT` | 1,073,152 | `de1232049704` | 25,721 | 128x8384, 256x4192, 512x2096, 1024x1048 |
| `WCCUPDAT.DAT` | 58,357,760 | `f7e4cf0a261f` | 319,563 | 128x455920, 256x227960, 512x113980, 1024x56990 |
| `WCCUSERS.DAT` | 10,240 | `88d6a7002a20` | 0 | 128x80, 256x40, 512x20, 1024x10 |


## Early decoding conclusions

- `WCCSHOPS.DAT` is strongly block-oriented. Shop names align to 512-byte records; for example, `general store` appears at offset 1546, consistent with record/block 3 plus a small name offset.
- `WCCSPELS.DAT` shows 256-byte indexed spell records for the base spell set. Several spell names match the CSV export using `offset = spellNumber * 256 + approximately 1032`.
- `WCCITEMS.DAT` is 1,372 exact 1024-byte blocks. Base item names align with 1024-byte blocks for a partial/base item range. The full CSV item count is larger, so expansion/update data likely lives in `WCCUPDAT.DAT` and/or other runtime files.
- `WCCRACE.DAT` appears to use 128-byte style record regions after an initial header/index area. Race names are present and match the CSV export.
- `WCCCLASS.DAT` contains all 15 class names. The names appear in a compact region with consistent spacing, but the full class binary structure still needs field mapping.
- `WCCMP001.DAT` and `WCCUPDAT.DAT` are large block-oriented files and contain many readable game strings. They require staged decoding because they likely contain multiple internal sections, not one simple table.

## Decode rule

Unknown bytes are not discarded. Until a field is understood, the decoder must preserve raw offsets/bytes in `unknownFields` or raw record blobs.

## Current status

This version adds repeatable tooling and reports so future database versions can finish one file type at a time with proof instead of guesses.
