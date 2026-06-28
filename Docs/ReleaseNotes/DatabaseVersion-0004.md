# Database Version 0004 - Native DAT Decode Foundation

## Added

- Native `.DAT` file catalog and hash report.
- String catalogs for every uploaded legacy runtime `.DAT` file.
- Binary block sample reports.
- CSV-to-DAT name offset crosscheck report.
- `Tools/Importer/ReforgedDatDecoder.py` inspection tool.
- Documentation for current native `.DAT` decoding status.

## Safety

- Runs from the extracted database-version folder.
- Auto-locates the project root.
- Does not require Access, OLEDB, or Office.
- Does not overwrite `Saves/`.
- Does not modify original imported `.DAT` files.
- Does not claim full binary decoding is complete.
