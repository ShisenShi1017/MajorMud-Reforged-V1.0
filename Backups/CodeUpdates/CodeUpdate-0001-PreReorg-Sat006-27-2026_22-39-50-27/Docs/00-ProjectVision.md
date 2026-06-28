# MajorMud Reforged Project Vision

MajorMud Reforged is a standalone MajorMud server project with editor support first and MegaMud 2.0-style client work later.

## Current Data Vision

Original MajorMud files are import/reference sources only. The active game database belongs in `Data/` in simple, editable Reforged formats. After conversion, the server must not require Access, OLEDB, MDB, ACCDB, or native `.DAT` files to run.

## Runtime File Milestone

Database Version 0003 archives the native MajorMud runtime `.DAT` set in `ImportedDatabases/NativeRuntime/` and creates the import/audit foundation for decoding those files into standalone editable data.

## Long-Term Direction

1. Preserve original source files unchanged.
2. Decode every supported source record losslessly.
3. Preserve unknown fields until identified.
4. Store editable canonical data under `Data/`.
5. Make the server load only Reforged data.
6. Make the editor edit Reforged data through safe tooling/server APIs.
