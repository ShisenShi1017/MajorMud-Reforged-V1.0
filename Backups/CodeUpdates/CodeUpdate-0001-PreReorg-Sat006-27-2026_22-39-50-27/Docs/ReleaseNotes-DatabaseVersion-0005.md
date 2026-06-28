# Release Notes - Database Version 0005

## Added

- `Tools/Importer/native_dat_probe.py`
- Native DAT manifest and string catalogs.
- Fixed-block record catalogs for shops, spells, and items.
- CSV crosscheck reports for classes, races, shops, spells, and items.
- Updated DAT decoding documentation.

## Safety

- Runs from its own extracted folder.
- Does not require manual project path entry.
- Does not modify saves.
- Does not modify original imported source data.

## Known Limitations

- Full native DAT decoding is still in progress.
- Large map/update files are not fully decoded yet.

