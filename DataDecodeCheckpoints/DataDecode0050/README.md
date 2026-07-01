# DataDecode0050 Repository Preservation

This folder preserves the user-provided `DataDecode0050-WGSERVEREventMessageDispatcherCorrelation-PreMirror.zip` package in the repository.

## Source Package

- Source path: `C:\Users\djkao\Downloads\DataDecode0050-WGSERVEREventMessageDispatcherCorrelation-PreMirror.zip`
- ZIP SHA-256: `D4B23EF3F94BDEA918D33025A472259EDF675F26F5746BE6B24A983527E61890`
- Preserved file count: 18 package files, plus this README and the earlier transition notes under `Transition/`.

## WGSERVER Reference

- Source executable: `C:\Worldgroup Server\wgserver.exe`
- Size: 432,128 bytes
- SHA-256: `4da28d718cf99701aa63c1889770f293bb3ec828aea5e8bea500473d2698c73f`
- Policy: reference/analysis evidence only. Do not execute, patch, distribute, or make runtime use of this executable.

## Summary

DataDecode0050 is a static-analysis-only WGSERVER event/message dispatcher correlation pass. It reports:

- 7 PE sections.
- 5,916 ASCII strings.
- 39 UTF-16LE strings.
- 274 event/message/schema-focused strings.
- 86 string pointer cross references.
- 86 candidate dispatch/reference windows.
- 1,739 WCCMSG/WCCTEXT message/textblock anchor rows.
- 850 cross-file U16 candidate rows.
- 0 canonical promotions.

These results are candidate evidence only. They do not prove dispatch behavior or field semantics without disassembly-level or runtime corroboration.

## Transition Note

CodeUpdate-0047 originally added a DataDecode0050 GitHub transition handoff before the real DataDecode0050 package was supplied. Those transition files are preserved under `Transition/` for historical continuity.
