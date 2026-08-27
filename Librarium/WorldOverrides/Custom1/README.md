# Custom World 1 overrides

`overrides.json` is the canonical host-authored delta for the first custom world.

- Base world: Classic
- Runtime world: Custom1
- Generated output: `Data/Custom1`
- Classic never consumes this file.
- The Editor writes patches here, then the compiler rebuilds the Custom1 runtime projection.

Supported operations in this first editor pass are `patch`, `upsert`, and `delete`. Patch fields are recursively merged into the selected Librarium runtime record. Generated `/Data` remains disposable and must not be hand-edited.
