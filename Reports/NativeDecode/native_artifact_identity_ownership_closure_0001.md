# Native Artifact Identity and Ownership Closure 0001

## Scope

This pass establishes the authoritative inventory, identity, WCC/WBB pairing, PE linkage, and first host-ownership boundary for the supplied MajorMUD 1.11p native artifacts. It does not assign gameplay meaning to untraced native call sites.

## Proven findings

- Cataloged **396** supplied native/data artifacts: 147 WCC, 234 WBB, and 15 NativeRuntime files.
- Recovered **147** canonical WCC/WBB filename pairs. **111** are byte-identical and **36** differ.
- Both distributions contain the three executable modules: `MMUD.DLL`, `MMPLS.DLL`, and `MMUTL.EXE`.
- The WCC and WBB executable counterparts have equal file sizes and equal export counts, but distinct hashes. They must not be treated as interchangeable binaries.
- `MMUD.DLL` imports `WGSERVER.EXE` directly. This proves a native Worldgroup host boundary for terminal/session state, DFA storage, host messaging, time, and related services exposed through imported symbols.
- `MMPLS.DLL` imports its distribution-specific `WCCMMUD.DLL` or `WBBMMUD.DLL`, proving that the player/session shim is coupled to the matching distribution module.
- `MMUTL.EXE` imports Btrieve (`wbtrv32.dll`) and Winsock, identifying it as an external database/network utility boundary rather than the main in-process gameplay dispatcher.

## Binary comparison

- `mmud.dll`: 643072 bytes; 172 differing byte positions; first differences at offsets 0x78ACA, 0x78AD7, 0x78B01, 0x78B02, 0x78B0D, 0x78B0E, 0x78B26, 0x78B27, 0x78B34, 0x78B35, 0x78B4C, 0x78B4D, 0x78B59, 0x78B5A, 0x78B71, 0x78B72, 0x78B84, 0x78B85, 0x78B91, 0x78B92.
- `mmpls.dll`: 28672 bytes; 21 differing byte positions; first differences at offsets 0x456B, 0x45E0, 0x45E1, 0x4677, 0x4678, 0x4684, 0x4685, 0x469D, 0x469E, 0x46AD, 0x46D3, 0x46D4, 0x4AAD, 0x519C, 0x519D, 0x58B5, 0x58B6, 0x592F, 0x5930, 0x59C8.
- `mmutl.exe`: 106496 bytes; 100 differing byte positions; first differences at offsets 0x13A62, 0x13A63, 0x13A89, 0x13A8A, 0x13B0C, 0x13B0D, 0x13B19, 0x13B1A, 0x13BFD, 0x13BFE, 0x13C53, 0x13C54, 0x13CDC, 0x13CDD, 0x13CE9, 0x13CEA, 0x13CF6, 0x13CF7, 0x13D03, 0x13D04.

## Runtime policy applied

- Preserve WCC and WBB artifacts as separate evidence families.
- Bind provenance to exact SHA-256, size, canonical pair, and distribution path.
- Do not promote gameplay behavior from symbol names alone. Call-site disassembly or executable observation remains required.
- Treat Worldgroup-owned services as host boundary requests until their call semantics are proven.

## Next native closure target

Trace the seven room latch identifiers and `remoteaction` through the exported MMUD entry points, native strings, WGSERVER imports, and DAT record references. The supplied native modules are sufficient to begin that call-path pass; the Worldgroup host executable itself is still required for complete semantics of imported host functions.
