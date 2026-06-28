# Database Version 0004 Audit - Native DAT Decode Foundation

## Purpose

Start native MajorMud `.DAT` decoding for the standalone Reforged database effort.

## What this version does

- Catalogs every uploaded native runtime `.DAT` file.
- Generates SHA-256 hashes and file sizes.
- Extracts readable string catalogs for every `.DAT` file.
- Produces block/sample reports for binary layout analysis.
- Cross-checks CSV record names against native `.DAT` offsets where matching CSV tables exist.
- Adds a repeatable Python decoder/inspection tool under `Tools/Importer`.
- Updates documentation and session handoff files.

## What this version does not claim

- It does not claim full `.DAT` decoding is complete.
- It does not replace the verified CSV-converted data yet.
- It does not modify saves.
- It does not overwrite original imported runtime files.

## Source files analyzed

- `WCCACMSR.DAT` - 6,144 bytes - SHA256 `ce9790c179034788f95366c659ca21931461acfc4c89462e2a3bba58dbb2de14`
- `WCCACTS.DAT` - 65,536 bytes - SHA256 `baf14617411fd3e464a424f3a744baa6f42651d0f3d2b5600657d77c58b1ae86`
- `WCCBANKS.DAT` - 1,536 bytes - SHA256 `58b3fa431854beb8bb5f921d47244c7008bbda46617da9895b2375cec6ab03ff`
- `WCCCLASS.DAT` - 6,144 bytes - SHA256 `8cc91e569b45c8b238b3ce820f00aef77aeb33bbdf7adfdc035fc732474d2fb4`
- `WCCGANGS.DAT` - 2,048 bytes - SHA256 `19ab7942a50ede6797bb3e263087f2f0bb5f205414103db977f65177b6b9c97f`
- `WCCITEMS.DAT` - 1,404,928 bytes - SHA256 `22d54b7aab28e1db5fd5fca9da3aa7f5e9867f671239ee22a306f05e0c4f1e62`
- `WCCKNMSR.DAT` - 561,152 bytes - SHA256 `b33f96d837052006a29e708511a8af8022e56c992b5df3483e02e5dcf3f3149c`
- `WCCMP001.DAT` - 30,973,440 bytes - SHA256 `17442fe74d608511b31cf22ca7f0924310acfbecdef49984a9e8cbd4209dc3c9`
- `WCCMSG.DAT` - 870,912 bytes - SHA256 `37546e9bdc91ffb006c761cdd557d3114f776c152e10b3ef53303b4826bc0f0c`
- `WCCRACE.DAT` - 3,072 bytes - SHA256 `1c227140da2fa8e6863ce81a90d061178aacc19bfa683e475728918ccbafed4e`
- `WCCSHOPS.DAT` - 78,336 bytes - SHA256 `1d5d8bd676717b1ffb8e10adf41123cbb3001c1d6750ef297ebffc56b46ed012`
- `WCCSPELS.DAT` - 256,512 bytes - SHA256 `131d9221f12e4ff09a2be53c5456c504ecf24a15a526a12363efec4d0e21ebd7`
- `WCCTEXT.DAT` - 1,073,152 bytes - SHA256 `de1232049704ccbea60421ad48263b7f198de267feaafa8507f6d4e706bcd9cf`
- `WCCUPDAT.DAT` - 58,357,760 bytes - SHA256 `f7e4cf0a261ff38134dd7df40502ae8c46b55651056e859aaa3853583c43b235`
- `WCCUSERS.DAT` - 10,240 bytes - SHA256 `88d6a7002a20ed1b228ec8ad8b6bbf0731a33c2ce6a76b5f2656971f33c7d73d`


## Verification outputs

- `Data/NativeDecodeReports/native_dat_file_catalog.json`
- `Data/NativeDecodeReports/csv_name_offset_crosscheck.json`
- `Data/NativeDecodeReports/StringCatalogs/*.tsv`
- `Data/NativeDecodeReports/Samples/*.json`

## Next recommended database version

Database Version 0005 should fully decode one or two small stable files first, preferably:

1. `WCCRACE.DAT`
2. `WCCCLASS.DAT`
3. `WCCSHOPS.DAT`

After those are fully mapped and verified, move to spells, items, messages/text, map, and update files.
