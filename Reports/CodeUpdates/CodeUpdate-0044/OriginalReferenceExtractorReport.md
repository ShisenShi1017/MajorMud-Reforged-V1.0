# CodeUpdate-0044 Original Reference Extractor Report

## Summary

CodeUpdate-0044 adds a reference-only extraction/capture tool for original Worldgroup, WCC, WBB, WSGSERVE, and related source material.

The tool is intended to support future ChatGPT-guided extraction and mapping work. It does not change canonical Reforged `Data/`.

## Verification Performed

- Ran `Tools/ReferenceCapture/Capture-OriginalReferenceData.ps1` against `Tools/ReferenceCapture/README.md`.
- Confirmed the self-test completed.
- Confirmed the generated manifest reported:
  - Capture name: `ToolSelfTest`
  - Files inventoried: `1`
  - Printable strings captured: `35`
  - Policy: reference-only; no original programs executed; no keygen used; no Reforged `Data/` modified.
- Removed temporary self-test output from:
  - `ImportedDatabases/ReferenceCaptures/ToolSelfTest/`
  - `Reports/ReferenceCaptures/ToolSelfTest/`

## Local Build Verification

Final local .NET verification is recorded in `UpdateManifest.json` after verification commands complete.

## Boundaries Confirmed

- No `Data/` files were intentionally modified.
- No `ImportedDatabases/` source material was deleted.
- No legacy executable was run.
- No keygen was used.
- No gameplay, networking, editor, or world simulation behavior was added.

## Known Limitations

- The extractor is broad evidence capture, not complete structured proprietary format decoding.
- RAR and 7z extraction depends on a local 7-Zip installation.
- Any actual data reconciliation must be handled by a later reviewed and approved update.
