# Database Audit - Database Version 0003

## Purpose

Archive/catalog the complete uploaded native MajorMud runtime `.DAT` file set and establish the no-Access import foundation.

## Source Files Cataloged

| File | Size Bytes | SHA-256 | First 16 Bytes |
|---|---:|---|---|
| `WCCACMSR.DAT` | 6,144 | `ce9790c179034788f95366c659ca21931461acfc4c89462e2a3bba58dbb2de14` | `00 00 00 00 00 00 00 04 00 08 00 00 FF FF FF FF` |
| `WCCACTS.DAT` | 65,536 | `baf14617411fd3e464a424f3a744baa6f42651d0f3d2b5600657d77c58b1ae86` | `00 00 00 00 64 06 00 04 00 04 00 00 FF FF FF FF` |
| `WCCBANKS.DAT` | 1,536 | `58b3fa431854beb8bb5f921d47244c7008bbda46617da9895b2375cec6ab03ff` | `00 00 00 00 00 00 00 04 00 02 00 00 FF FF FF FF` |
| `WCCCLASS.DAT` | 6,144 | `8cc91e569b45c8b238b3ce820f00aef77aeb33bbdf7adfdc035fc732474d2fb4` | `00 00 00 00 31 00 00 04 00 06 00 00 FF FF FF FF` |
| `WCCGANGS.DAT` | 2,048 | `19ab7942a50ede6797bb3e263087f2f0bb5f205414103db977f65177b6b9c97f` | `00 00 00 00 00 00 00 04 00 02 00 00 FF FF FF FF` |
| `WCCITEMS.DAT` | 1,404,928 | `22d54b7aab28e1db5fd5fca9da3aa7f5e9867f671239ee22a306f05e0c4f1e62` | `00 00 00 00 1C 61 00 04 00 08 00 00 FF FF FF FF` |
| `WCCKNMSR.DAT` | 561,152 | `b33f96d837052006a29e708511a8af8022e56c992b5df3483e02e5dcf3f3149c` | `00 00 00 00 06 50 00 04 00 08 00 00 FF FF FF FF` |
| `WCCMP001.DAT` | 30,973,440 | `17442fe74d608511b31cf22ca7f0924310acfbecdef49984a9e8cbd4209dc3c9` | `00 00 00 00 0D 56 00 04 00 06 00 00 FF FF FF FF` |
| `WCCMSG.DAT` | 870,912 | `37546e9bdc91ffb006c761cdd557d3114f776c152e10b3ef53303b4826bc0f0c` | `00 00 00 00 B1 17 00 04 00 02 00 00 FF FF FF FF` |
| `WCCRACE.DAT` | 3,072 | `1c227140da2fa8e6863ce81a90d061178aacc19bfa683e475728918ccbafed4e` | `00 00 00 00 86 00 00 04 00 02 00 00 FF FF FF FF` |
| `WCCSHOPS.DAT` | 78,336 | `1d5d8bd676717b1ffb8e10adf41123cbb3001c1d6750ef297ebffc56b46ed012` | `00 00 00 00 43 03 00 04 00 02 00 00 FF FF FF FF` |
| `WCCSPELS.DAT` | 256,512 | `131d9221f12e4ff09a2be53c5456c504ecf24a15a526a12363efec4d0e21ebd7` | `00 00 00 00 A6 15 00 04 00 02 00 00 FF FF FF FF` |
| `WCCTEXT.DAT` | 1,073,152 | `de1232049704ccbea60421ad48263b7f198de267feaafa8507f6d4e706bcd9cf` | `00 00 00 00 2C 58 00 04 00 08 00 00 FF FF FF FF` |
| `WCCUPDAT.DAT` | 58,357,760 | `f7e4cf0a261ff38134dd7df40502ae8c46b55651056e859aaa3853583c43b235` | `00 00 00 00 06 5C 00 04 00 08 00 00 FF FF FF FF` |
| `WCCUSERS.DAT` | 10,240 | `88d6a7002a20ed1b228ec8ad8b6bbf0731a33c2ce6a76b5f2656971f33c7d73d` | `00 00 00 00 00 00 00 04 00 08 00 00 FF FF FF FF` |

## Duplicate Check

- Native source filenames cataloged: 15
- Duplicate source filenames in this package: 0

This version does not export decoded record IDs from the `.DAT` files, so decoded-record duplicate checks are pending until native decoders exist.

## CSV Reference Counts

| CSV Table | Records | Columns |
|---|---:|---:|
| `classes.TXT` | 15 | 30 |
| `items.TXT` | 2,017 | 95 |
| `lairs.TXT` | 421 | 15 |
| `monsters.TXT` | 1,126 | 134 |
| `races.TXT` | 13 | 37 |
| `rooms.TXT` | 26,666 | 21 |
| `shops.TXT` | 178 | 109 |
| `spells.TXT` | 1,458 | 46 |
| `tbinfo.TXT` | 2,279 | 4 |

## Verification Statement

All uploaded native runtime `.DAT` files available in this chat were copied into the package and cataloged by filename, byte size, SHA-256 hash, and first 16 bytes.

## Not Yet Claimed

- Full native `.DAT` decoding is not complete.
- Field maps are not complete.
- Native record counts are not yet verified against CSV exports.
- The standalone server has not yet been changed to load decoded native data from `Data/`.
