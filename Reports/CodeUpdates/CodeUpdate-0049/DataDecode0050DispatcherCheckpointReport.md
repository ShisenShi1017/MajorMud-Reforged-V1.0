# CodeUpdate-0049 DataDecode0050 Dispatcher Checkpoint Report

## Summary

CodeUpdate-0049 preserves the real DataDecode0050 WGSERVER Event & Message Dispatcher Correlation checkpoint in the repository.

CodeUpdate-0047 previously recorded a DataDecode0050 GitHub transition handoff before the real package was supplied. Those transition notes are preserved under `DataDecodeCheckpoints/DataDecode0050/Transition/`, while the root `DataDecodeCheckpoints/DataDecode0050/` folder now contains the actual DataDecode0050 package files.

## Preserved Evidence

DataDecode0050 source package:

- Path: `C:\Users\djkao\Downloads\DataDecode0050-WGSERVEREventMessageDispatcherCorrelation-PreMirror.zip`
- SHA-256: `D4B23EF3F94BDEA918D33025A472259EDF675F26F5746BE6B24A983527E61890`
- Preserved under: `DataDecodeCheckpoints/DataDecode0050/`
- Package file count: 18.

WGSERVER reference executable:

- Path: `C:\Worldgroup Server\wgserver.exe`
- Size: 432,128 bytes.
- SHA-256: `4da28d718cf99701aa63c1889770f293bb3ec828aea5e8bea500473d2698c73f`
- Not copied or executed by this update.

## DataDecode0050 Findings

- Parsed PE32 section layout with 7 sections.
- Extracted 5,916 ASCII strings.
- Extracted 39 UTF-16LE strings.
- Identified 274 event/message/schema-focused executable strings.
- Found 86 literal pointer cross references to focused strings.
- Built 86 candidate dispatch/reference windows.
- Cataloged 1,739 WCCMSG/WCCTEXT message/textblock anchors.
- Built 850 cross-file U16 candidate rows.
- Promoted 0 canonical fields.

## HTTrack/OmniVerse Impact

The CodeUpdate0046 HTTrack/OmniVerse reference capture helps identify coverage gaps and comparison targets, but it does not prove dispatcher behavior or binary field semantics.

For DataDecode0050 specifically, the authoritative evidence remains `wgserver.exe`, NativeRuntime DAT files, and prior WGSERVER record-access correlation. OmniVerse remains validation/correlation evidence only.

## Boundaries

- No canonical `Data/` rewrite.
- No field promotions.
- No server behavior changes.
- No gameplay systems.
- No execution or copying of `wgserver.exe`.
- No OmniVerse-only promotion.
- No U16/U32-only promotion.

## Next Recommended Decode Pass

DataDecode0051 - WGSERVER Combat Routine Correlation:

- Damage calculations.
- Armor calculations.
- Spell calculations.
- Resistance calculations.
- Ability calculations.

Promote no fields without binary-backed proof.
