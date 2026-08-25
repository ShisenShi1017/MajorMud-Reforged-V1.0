# Native Presentation Reproducibility Closure — CodeUpdate0139

Date: 2026-07-29

## Result

The verified WCCTEXT2 payload catalog is now canonical Librarium data instead of an
ignored generated artifact that disappears during a clean compile.

- Source evidence: `ImportedDatabases/WCCDefaultInstall/wcc default/wcctext2.dat`
- Source SHA-256: `fadca283e75df5d07c1812133644c19c5cf8915e56e4968a82922d4ddacaaef8`
- Promoted records: `3,467`
- Librarium family: `native_textblock_payloads`
- Generated runtime file: `Data/Classic/native_textblock_payloads.json`
- Runtime authority: `RuntimeClassicNativeTextblockPayloadAuthority`

`Tools/Promote-Wcctext2Payloads.ps1` reproduces the existing verified 0x800-byte
page decoder, requires the 0x5600 record marker, rejects duplicate/non-positive
textblock IDs, preserves payload/page evidence, and refuses any record count other
than 3,467. The source database is read-only and was not modified.

## Verification

- Classic clean compile: PASS — 30 families, 101,731 objects, 35 files.
- Server Release build: PASS — 0 warnings, 0 errors.
- Bundle F10 native payload audit: PASS — 3,467 payloads, 0 disconnects.
- Bundle F10 complete dialogue audit: PASS — 754/754 presentation references
  renderable, 0 disconnects.
- Interaction/narrative smoke: PASS.

## Evidence Boundary

The four decoded Map 17 pedestal scripts preserve ordered `message` identifiers
3318–3322. Those identifiers are not proven to use the WCCTEXT2 textblock namespace;
the corresponding WCCTEXT2 records contain unrelated scripts or screen payloads.
They therefore remain structurally preserved and are not falsely rendered through
this catalog. Exact WCCMSG2 logical-ID correlation remains the next evidence task.
