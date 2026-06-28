# Reforged Native Runtime Importer Tools

This folder starts the permanent no-Access import tooling for Reforged.

## Current Tool

`Catalog-NativeRuntime.ps1` catalogs native `.DAT` files by size, SHA-256 hash, and first-byte signature.

Run from the project root after applying Database Version 0003:

```powershell
powershell -ExecutionPolicy Bypass -File Tools\Importer\Catalog-NativeRuntime.ps1
```

## Future Work

Future database versions should add actual decoders one file type at a time and verify decoded records against the CSV/reference data before marking them complete.
