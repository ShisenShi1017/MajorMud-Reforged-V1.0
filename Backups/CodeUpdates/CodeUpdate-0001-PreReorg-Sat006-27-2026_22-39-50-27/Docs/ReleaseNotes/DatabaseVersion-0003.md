# Release Notes - Database Version 0003

## Name

Native Runtime Data Foundation

## Type

Database Version

## Summary

Adds the uploaded native MajorMud runtime `.DAT` source set to the project as reference/import data and adds importer/catalog tooling and documentation.

## Added

- `ImportedDatabases/NativeRuntime/*.DAT`
- `Data/NativeRuntimeCatalog/SourceFileCatalog.json`
- `Tools/Importer/Catalog-NativeRuntime.ps1`
- `Tools/Importer/README.md`
- Database audit report
- Database format documentation updates
- Chat/development rules documentation

## Safety

- Does not overwrite `Saves/`.
- Does not modify server code.
- Does not require Access/OLEDB.
- Does not claim full native decoding is complete.
