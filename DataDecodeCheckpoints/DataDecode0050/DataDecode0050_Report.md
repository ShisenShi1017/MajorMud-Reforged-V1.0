# DataDecode0050 — WGSERVER Event & Message Dispatcher Correlation

## Scope
Static-analysis-only correlation of `wgserver.exe` event/message/textblock references against decoded native datasets and prior 0049 record-access windows. No runtime execution was performed.

## Inputs
- `wgserver.exe` (432128 bytes, SHA-256 `4da28d718cf99701aa63c1889770f293bb3ec828aea5e8bea500473d2698c73f`)
- Repository NativeRuntime DAT files
- DataDecode0049 WGSERVER record-access checkpoint

## Findings
- Parsed PE32 section layout with 7 sections.
- Extracted 5916 ASCII strings and 39 UTF-16LE strings.
- Identified 274 event/message/schema-focused executable strings.
- Found 86 literal pointer xrefs to focused strings.
- Built 86 candidate dispatch/reference windows around those xrefs.
- Cataloged 1739 WCCMSG/WCCTEXT message/textblock anchors from native DAT and decode catalogs.
- Built 850 cross-file U16 candidate rows linking text/quest seed values to native DAT occurrences.

## Interpretation
0050 narrows likely executable regions that reference messages, textblocks, quest flags, events, triggers, and related decoded schema terms. These are static candidate windows, not confirmed dispatch routines. The pass provides a prioritized target list for future disassembly or runtime work while preserving the no-guess rule.

## Promotions
No canonical fields were promoted. String xrefs and raw numeric values are still noisy without disassembly-level or runtime corroboration.

## Next Pass
DataDecode0051 should focus on WGSERVER combat routine correlation using the 0047 combat evidence and executable candidate windows.
