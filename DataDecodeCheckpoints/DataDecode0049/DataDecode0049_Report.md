# DataDecode0049 — WGSERVER Record Access / Structure Correlation

## Scope
Static-analysis-only executable correlation against `wgserver.exe` and NativeRuntime DAT datasets. This pass follows DataDecode0048 by moving from loader/string discovery into record-size, record-count, and candidate record-access windows.

## Inputs
- `wgserver.exe` (432128 bytes)
- Repository NativeRuntime DAT files
- DataDecode0048 executable-loader checkpoint

## Findings
- Profiled 15 NativeRuntime DAT datasets.
- Extracted 5916 executable ASCII strings length >= 4.
- Produced 9574 record-size/count/file-size literal hits.
- Produced 29 string-address xrefs and 29 record-access candidate windows.
- Built 15 dataset record-descriptor candidate rows.
- Built 93 canonical term correlation hits.

## Interpretation
This pass identifies static windows where the executable appears to reference dataset filenames or decoded schema terms near constants that match DAT file sizes, inferred record sizes, or inferred record counts. These are candidate record-access/loader windows, not proof of field semantics.

## Promotions
No canonical fields were promoted. Raw literals and string xrefs remain noisy without disassembly-level confirmation.

## Next Pass
DataDecode0050 should focus on static event/message dispatcher correlation using the highest-priority executable windows from 0048–0049.
