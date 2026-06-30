# CodeUpdate-0038 - Server Bootstrap Directory Status

## Purpose

CodeUpdate-0038 continues the small Milestone 2 host/server bootstrap path. It makes required directory verification visible in the bootstrap report and startup output so future host tooling can inspect repository readiness without parsing validation errors.

## Changed

- Added `RequiredDirectoryStatus` records to the Server bootstrap report.
- Added required-directory total, present, and missing counts to `ServerBootstrapReport`.
- Updated Server startup output to print required-directory status.
- Added smoke-test coverage for required-directory report status.
- Recorded CodeUpdate-0037 GitHub Actions as green by user report.

## Boundaries

- No gameplay systems are implemented.
- No combat is implemented.
- No character creation is implemented.
- No world simulation loop is implemented.
- No networking expansion is implemented.
- No live editor implementation is added.
- No `Data/` files are rewritten.
- No `ImportedDatabases/` files are overwritten.

## Verification

Local verification is recorded in `UpdateManifest.json`. GitHub Actions is green by user report.

## Next Recommended Work

After CodeUpdate-0038 is pushed and GitHub Actions is green, continue with one small approved Milestone 2 host/server slice. A safe candidate is adding a non-gameplay bootstrap readiness summary object for host/editor tooling.
