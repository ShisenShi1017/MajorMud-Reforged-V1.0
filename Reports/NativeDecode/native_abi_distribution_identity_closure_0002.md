# Native ABI and Distribution Identity Closure 0002

## Proven closure

- `MMUD.DLL`, `MMPLS.DLL`, and `MMUTL.EXE` preserve the same PE import/export ABI across WCC and WBB counterparts.
- The paired files preserve equal sizes and export counts: MMUD 643072 bytes/592 exports, MMPLS 28672 bytes/26 exports, and MMUTL 106496 bytes/74 exports.
- Byte-range inspection classified all WCC/WBB differences as embedded distribution-name/path substitutions (`WCC`/`WBB`, `wcc`/`wbb`, and associated single-character substitutions). The changed byte windows decode as embedded distribution identity/path text. Instruction-level equivalence remains unclaimed until call-site disassembly establishes instruction boundaries.
- WCC and WBB therefore represent distinct provenance identities over a shared native ABI. Runtime selection must validate the exact distribution hash while using the same recovered ABI ownership model.
- `MMPLS.DLL` remains distribution-bound by its import name (`WCCMMUD.DLL` versus `WBBMMUD.DLL`), even though the imported module ABI is shared.

## Runtime implementation

`NativeModuleAbiRegistry` now records the six exact module identities, expected sizes, imports, export counts, shared-ABI status, and evidence identifiers. It rejects unknown or hash-mismatched native artifacts instead of silently treating WCC/WBB files as interchangeable.

## Still unresolved

- WGSERVER host function bodies, dispatcher scheduling, terminal/session side effects, and host persistence semantics cannot be recovered from import names alone.
- Gameplay call-path ownership, seven room latch effects, `remoteaction`, random generation, save serialization, checksums, migration, and crash recovery still require call-site tracing and/or missing host/runtime artifacts.
