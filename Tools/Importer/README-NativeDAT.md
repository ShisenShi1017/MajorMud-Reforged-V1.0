# Reforged Native DAT Decoder

This folder contains conservative native `.DAT` decoding/inspection tools.

## Run manually

```bat
python Tools\Importer\ReforgedDatDecoder.py ImportedDatabases\NativeRuntime --out Data\NativeDecodeReports\ManualRun
```

The tool does not modify source `.DAT` files.

## Current purpose

Database Version 0004 uses this tool as the foundation for full native decoding. It currently produces file catalogs, string catalogs, and binary block samples. Future database versions will add complete field maps one file type at a time.
