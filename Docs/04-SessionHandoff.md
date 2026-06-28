# Session Handoff

## Current Focus

Native `.DAT` decoding for the standalone MajorMud Reforged database.

## Last Completed

Database Version 0005 adds a native DAT probe tool and decode reports.

## Key Findings

- Shops are the strongest next full decode target: 512-byte blocks, ID at `+6`, name at `+10`.
- Classes and races have complete CSV name matches but need final stride/field mapping.
- Spells and items are partially matched; missing CSV records likely involve update files or spelling/format differences.
- Large files `WCCMP001.DAT` and `WCCUPDAT.DAT` remain cataloged but not fully decoded.

## Next Recommended Task

Implement a focused `WCCSHOPS.DAT` structural decoder that maps each shop block to editable JSON and verifies against `shops.TXT` by shop number.

## Rules Reminder

All future code patches and database versions must apply from their own extracted folder using an `Apply-*.bat` file.

