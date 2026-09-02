# Reforged Roadmap

The current operating status is maintained in the root `PROJECT_STATUS.md`. This file contains only forward-looking architecture work.

## Current milestone

**CodeUpdate0141 - Validated Client and Repository Consolidation**

Automated baseline:

- current-health validator: 26/26 PASS;
- Reforged client smokes: 14/14 PASS;
- Classic parity: 200 PASS / 0 STRONG / 0 UNKNOWN;
- current local Release App/Server builds: 0 warnings / 0 errors.

## Immediate integration gates

1. Exercise the external-BBS overlay/import workflow against a real board MDB.
2. Exercise Creator Studio against a real custom-world MDB plus its matching WCC/WGS source set.
3. Review the resulting overlay/provenance/conflict output and test the generated/custom world in runtime.
4. Only after those real-file tests, label those import paths field-certified.

## Release preparation

After the real-file integration gates:

- perform the final repository cleanup;
- build the intended Release/installer artifacts;
- add publisher/version metadata and code signing when available;
- scan the exact release binaries and submit antivirus false positives when necessary;
- run the appropriate final build/smoke/validator gates for the source changes included in the release candidate.

## Ongoing architecture

Keep Reforged server-authoritative, keep Librarium as committed gameplay authority, keep generated `Data/` disposable, and keep client automation fail-closed with independent capability switches and Emergency Stop.

Detailed historical roadmap/update chains are intentionally left to Git history.
