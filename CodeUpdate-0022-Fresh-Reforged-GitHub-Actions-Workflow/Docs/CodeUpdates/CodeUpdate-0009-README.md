# CodeUpdate-0009 Package Notes

Apply from the extracted update folder by running:

`Apply-CodeUpdate-0009-Core-Audit-And-Test-Foundation.bat`

The script locates the project root by searching upward and nearby for `ProjectManifest.json`, `README.md`, `VERSION.md`, `CHANGELOG.md`, `Core/`, and `Data/`.

The package intentionally excludes:

- `Saves/`
- `ImportedDatabases/`

Normal runtime still uses compiled/runtime-ready artifacts from `Bin/`; this update does not add any SDK runtime path.
